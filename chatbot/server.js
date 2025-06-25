import express from 'express';
import bodyParser from 'body-parser';
import cors from 'cors';
// --- Paste your RAG logic imports and code here ---
import { createClient } from '@supabase/supabase-js';
import { SupabaseHybridSearch } from '@langchain/community/retrievers/supabase';
import { GoogleGenerativeAIEmbeddings } from '@langchain/google-genai';
import { ChatGoogleGenerativeAI } from '@langchain/google-genai';
import { GoogleGenerativeAI } from '@google/generative-ai';
import { PromptTemplate } from '@langchain/core/prompts';
import { RunnableSequence, RunnablePassthrough } from '@langchain/core/runnables';
import { StringOutputParser } from '@langchain/core/output_parsers';
import dotenv from 'dotenv';

// Load environment variables
dotenv.config();

// --- Begin your RAG logic (copy from your working code) ---
// Initialize Supabase client
const supabase = createClient(
  process.env.SUPABASE_URL || '',
  process.env.SUPABASE_PRIVATE_KEY || ''
);

const embeddings = new GoogleGenerativeAIEmbeddings({
  apiKey: process.env.GOOGLE_API_KEY,
  model: 'embedding-001',
});

const genAI = new GoogleGenerativeAI(process.env.GOOGLE_API_KEY);
const geminiModel = genAI.getGenerativeModel({ model: 'gemini-1.5-flash' });

const llm = new ChatGoogleGenerativeAI({
  apiKey: process.env.GOOGLE_API_KEY,
  model: 'gemini-1.5-flash',
  temperature: 0.2,
});

let priceDistributionCache = null;
let genresCache = null;

async function fetchPriceDistribution() {
  try {
    const { data, error } = await supabase.rpc('get_price_distribution');
    if (error) throw new Error(`Error fetching price distribution: ${error.message}`);
    if (!data || data.length === 0) return { min_price: 0, max_price: 100, low_price: 10, high_price: 50 };
    priceDistributionCache = data[0];
    return priceDistributionCache;
  } catch (err) {
    throw err;
  }
}

async function fetchGenres() {
  try {
    const { data, error } = await supabase.rpc('get_genres');
    if (error) throw new Error(`Error fetching genres: ${error.message}`);
    if (!data || data.length === 0) return ['sci-fi', 'fantasy', 'adventure', 'thriller', 'romance', 'mystery'];
    genresCache = data.map(item => item.genre);
    return genresCache;
  } catch (err) {
    throw err;
  }
}

async function rewriteQuery(query, genres) {
  const prompt = `
    Bạn là một trợ lý thông minh, chuyên xử lý truy vấn tìm kiếm sách. Nhiệm vụ của bạn là:
    1. Nếu truy vấn không phải tiếng Anh, dịch sang tiếng Anh.
    2. Phân tích ý định của truy vấn và trả về JSON với các trường:
       - translatedQuery: Truy vấn đã dịch sang tiếng Anh
       - price: "cheap", "medium", "expensive", hoặc null
       - genres: Thể loại phù hợp nhất từ danh sách {genres} dưới dạng mảng JSON (ví dụ: ["Adventure"]), hoặc null
       - author: Tên tác giả, hoặc null
       - other: Các yêu cầu khác (ví dụ: "new", "popular"), hoặc null
    3. Mở rộng truy vấn bằng các từ đồng nghĩa hoặc từ liên quan bằng tiếng Anh.

    **Truy vấn**: "{query}"
    **Danh sách thể loại**: {genres}
  `.replace('{query}', query).replace('{genres}', JSON.stringify(genres));

  try {
    const response = await geminiModel.generateContent({
      contents: [{ parts: [{ text: prompt }] }],
      generationConfig: {
        responseMimeType: 'application/json',
        responseSchema: {
          type: 'object',
          properties: {
            translatedQuery: { type: 'string' },
            price: { type: 'string', enum: ['cheap', 'medium', 'expensive'], nullable: true },
            genres: { type: 'string', nullable: true },
            author: { type: 'string', nullable: true },
            other: { type: 'string', nullable: true },
            expandedQuery: { type: 'string' }
          },
          required: ['translatedQuery', 'expandedQuery'],
          propertyOrdering: ['translatedQuery', 'price', 'genres', 'author', 'other', 'expandedQuery']
        }
      }
    });
    const jsonString = response.response.text();
    return JSON.parse(jsonString);
  } catch (err) {
    return {
      translatedQuery: query,
      price: null,
      genres: null,
      author: null,
      other: null,
      expandedQuery: query
    };
  }
}

async function parseQuery(query) {
  const priceDist = priceDistributionCache || await fetchPriceDistribution();
  const genres = genresCache || await fetchGenres();
  const rewritten = await rewriteQuery(query, genres);
  let maxPrice = null;
  let minPrice = null;
  let maxPages = null;
  let filter = {};
  if (rewritten.price) {
    if (rewritten.price === 'cheap') {
      maxPrice = priceDist.low_price;
    } else if (rewritten.price === 'medium') {
      minPrice = priceDist.low_price;
      maxPrice = priceDist.high_price;
    } else if (rewritten.price === 'expensive') {
      minPrice = priceDist.high_price;
    }
  } else if (query.toLowerCase().includes('under $')) {
    const priceMatch = query.match(/under \$(\d+)/);
    if (priceMatch) maxPrice = parseFloat(priceMatch[1]);
  }
  if (query.toLowerCase().includes('short')) {
    maxPages = 200;
  } else if (query.toLowerCase().includes('under') && query.toLowerCase().includes('pages')) {
    const pagesMatch = query.match(/under (\d+) pages/);
    if (pagesMatch) maxPages = parseInt(pagesMatch[1]);
  }
  if (rewritten.author) filter.author = rewritten.author;
  if (rewritten.genres) filter.genres = rewritten.genres;
  if (rewritten.other === 'new') filter.rating = '4.5';
  if (rewritten.other === 'popular') filter.numRatings = '1000';
  return {
    cleanedQuery: rewritten.expandedQuery || rewritten.translatedQuery,
    maxPrice,
    minPrice,
    maxPages,
    filter
  };
}

const retriever = new SupabaseHybridSearch(embeddings, {
  client: supabase,
  similarityK: 3,
  keywordK: 3,
  tableName: 'book_embeddings',
  similarityQueryName: 'match_books',
  keywordQueryName: 'kw_match_books',
});

const prompt = PromptTemplate.fromTemplate(`
Bạn là một trợ lý thông minh có nhiệm vụ gợi ý các cuốn sách trong cửa hàng đến với người mua.
Yêu cầu định dạng câu trả lời thật rõ ràng, chặt chẽ, dễ đọc, sử dụng markdown như sau:

- Nếu có nhiều sách, hãy trình bày mỗi cuốn sách thành một nhóm riêng biệt, bắt đầu bằng gạch đầu dòng (-) và xuống dòng.
- Trong mỗi nhóm, trình bày các thông tin theo thứ tự sau, mỗi thông tin một dòng riêng và xuống dòng sau đó:
  - **Tên sách:** ...
  - **Tác giả:** ...
  - **Giá:** ...
  - **Thể loại:** ...
  - **Mô tả:** ...
  - **Ngôn ngữ:** ...
  - **Đánh giá:** ...
  - **Số lượt đánh giá:** ...
- Nếu chỉ có một sách, vẫn trình bày theo nhóm như trên.
- Nếu không có sách phù hợp, hãy gợi ý các lựa chọn khác cũng theo định dạng nhóm trên.
- Luôn sử dụng in đậm (**...**) cho tiêu đề trường thông tin.
- Không trình bày thông tin thừa, không lặp lại, không giải thích lại yêu cầu.
- Đảm bảo có khoảng trắng giữa các nhóm sách để dễ theo dõi.
- Nếu có gợi ý thêm, trình bày ở cuối, cũng dùng markdown và gạch đầu dòng.

**Thông tin sách:**
{context}

**Câu hỏi/Yêu cầu:**
{question}

**Trả lời (bắt buộc trình bày đúng định dạng markdown, rõ ràng, nhóm từng sách, không giải thích lại yêu cầu):**
`);

const formatContext = (docs) => {
  return docs
    .map((doc) => {
      const metadata = doc.metadata;
      const genresString = Array.isArray(metadata.genres) ? metadata.genres.join(', ') : 'Unknown';
      return `Title: ${metadata.title}\nAuthor: ${metadata.author}\nPrice: $${metadata.price}\nPages: ${metadata.pages}\nDescription: ${metadata.description}\nGenres: ${genresString}\nLanguage: ${metadata.language}\nRating: ${metadata.rating}\nSeries: ${metadata.series || 'None'}\nNumber of Ratings: ${metadata.numRatings}`;
    })
    .join('\n\n');
};

const ragChain = RunnableSequence.from([
  {
    context: async (input) => {
      const { cleanedQuery, maxPrice, minPrice, maxPages, filter } = await parseQuery(input);
      const docs = await retriever.invoke(cleanedQuery, {
        similarityOptions: { max_price: maxPrice, min_price: minPrice, max_pages: maxPages, filter },
        keywordOptions: { max_price: maxPrice, min_price: minPrice, max_pages: maxPages, filter },
      });
      return formatContext(docs);
    },
    question: new RunnablePassthrough(),
  },
  prompt,
  llm,
  new StringOutputParser(),
]);

async function runRAG(query) {
  try {
    const response = await ragChain.invoke(query);
    return response;
  } catch (error) {
    throw error;
  }
}

(async () => {
  await fetchPriceDistribution();
  await fetchGenres();
})();

// --- Express API ---
const app = express();
app.use(cors({
  origin: 'http://127.0.0.1:5500'
}));
app.use(bodyParser.json());

app.post('/rag', async (req, res) => {
  const { query } = req.body;
  try {
    const result = await runRAG(query);
    res.json({ result });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

const PORT = process.env.CHATBOT_PORT || 3001;
app.listen(PORT, () => {
  console.log(`RAG chatbot service running on port ${PORT}`);
});
