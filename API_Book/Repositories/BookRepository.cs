// SIMPLIFIED BOOKREPOSITORY - Tránh lỗi reflection
// API_Book/Repositories/BookRepository.cs

using API_Book.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace API_Book.Repositories
{
    public class BookRepository : IBookRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BookRepository> _logger;

        // Track cache keys for manual cleanup
        private readonly HashSet<string> _cacheKeys = new HashSet<string>();
        private readonly object _cacheKeysLock = new object();

        public BookRepository(ApplicationDbContext context, IMemoryCache cache, ILogger<BookRepository> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        // HELPER: Add cache key tracking
        private void SetCache<T>(string key, T value, TimeSpan expiration)
        {
            _cache.Set(key, value, expiration);

            lock (_cacheKeysLock)
            {
                _cacheKeys.Add(key);
            }
        }

        // SIÊU TỐI ƯU CHO 30K+ SÁCH
        public async Task<IEnumerable<Book>> GetBooksPagedAsync(int page, int pageSize)
        {
            var cacheKey = $"books_page_{page}_{pageSize}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<Book>? cachedBooks))
            {
                return cachedBooks!;
            }

            var skip = (page - 1) * pageSize;
            var startTime = DateTime.UtcNow;

            try
            {
                // CHỈ SELECT CÁC FIELD CẦN THIẾT
                var books = await _context.Books
                    .AsNoTracking()
                    .Select(b => new Book
                    {
                        Id = b.Id,
                        Title = b.Title.Length > 50 ? b.Title.Substring(0, 50) + "..." : b.Title,
                        Author = b.Author,
                        Price = b.Price,
                        CoverImg = b.CoverImg,
                        Rating = b.Rating ?? 0
                    })
                    .OrderBy(b => b.Id)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                // Cache với expiration time khác nhau
                var cacheTime = page <= 3 ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(15);
                SetCache(cacheKey, books, cacheTime);

                var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation($"📚 Loaded page {page} with {books.Count()} books in {loadTime:F0}ms");

                return books;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading books page {page}");
                throw;
            }
        }

        // TỐI ƯU COUNT VỚI CACHE DÀI HẠN
        public async Task<int> GetTotalBooksCountAsync()
        {
            const string cacheKey = "total_books_count";

            if (_cache.TryGetValue(cacheKey, out int cachedCount))
            {
                return cachedCount;
            }

            try
            {
                var count = await _context.Books.CountAsync();

                // Cache count 30 phút vì ít thay đổi
                SetCache(cacheKey, count, TimeSpan.FromMinutes(30));

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total books count");
                return _cache.TryGetValue(cacheKey, out int fallbackCount) ? fallbackCount : 30000;
            }
        }

        // SEARCH SIÊU TỐI ƯU
        public async Task<IEnumerable<Book>> SearchBooksPagedAsync(string searchTerm, int page, int pageSize)
        {
            var cacheKey = $"search_{searchTerm.ToLower()}_{page}_{pageSize}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<Book>? cachedResults))
            {
                return cachedResults!;
            }

            var skip = (page - 1) * pageSize;
            var lowerSearch = searchTerm.ToLower();

            try
            {
                var books = await _context.Books
                    .AsNoTracking()
                    .Where(b =>
                        b.Title.ToLower().Contains(lowerSearch) ||
                        b.Author.ToLower().Contains(lowerSearch) ||
                        (b.Genres != null && b.Genres.ToLower().Contains(lowerSearch)))
                    .Select(b => new Book
                    {
                        Id = b.Id,
                        Title = b.Title,
                        Author = b.Author,
                        Price = b.Price,
                        CoverImg = b.CoverImg,
                        Rating = b.Rating
                    })
                    .OrderBy(b =>
                        b.Title.ToLower().StartsWith(lowerSearch) ? 0 :
                        b.Author.ToLower().StartsWith(lowerSearch) ? 1 : 2)
                    .ThenBy(b => b.Title)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                // Cache search results 10 phút
                SetCache(cacheKey, books, TimeSpan.FromMinutes(10));

                return books;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching books with term: {searchTerm}");
                throw;
            }
        }

        // TOP RATED VỚI OPTIMIZATION
        public async Task<IEnumerable<Book>> GetTopRatedBooksPagedAsync(int page, int pageSize)
        {
            var cacheKey = $"top_rated_{page}_{pageSize}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<Book>? cachedBooks))
            {
                return cachedBooks!;
            }

            var skip = (page - 1) * pageSize;

            try
            {
                var books = await _context.Books
                    .AsNoTracking()
                    .Where(b => b.Rating.HasValue && b.Rating.Value >= 4.0f)
                    .Select(b => new Book
                    {
                        Id = b.Id,
                        Title = b.Title,
                        Author = b.Author,
                        Price = b.Price,
                        CoverImg = b.CoverImg,
                        Rating = b.Rating
                    })
                    .OrderByDescending(b => b.Rating)
                    .ThenByDescending(b => b.Id)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                // Cache 20 phút cho top rated
                SetCache(cacheKey, books, TimeSpan.FromMinutes(20));

                return books;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading top rated books page {page}");
                throw;
            }
        }

        // BOOK DETAILS VỚI CACHE
        public async Task<Book?> GetBookByIdAsync(int id)
        {
            var cacheKey = $"book_details_{id}";

            if (_cache.TryGetValue(cacheKey, out Book? cachedBook))
            {
                return cachedBook;
            }

            try
            {
                var book = await _context.Books
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (book != null)
                {
                    // Cache chi tiết sách 1 giờ
                    SetCache(cacheKey, book, TimeSpan.FromHours(1));
                }

                return book;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading book {id}");
                return null;
            }
        }

        // SEARCH COUNT
        public async Task<int> GetSearchResultsCountAsync(string searchTerm)
        {
            var cacheKey = $"search_count_{searchTerm.ToLower()}";

            if (_cache.TryGetValue(cacheKey, out int cachedCount))
            {
                return cachedCount;
            }

            try
            {
                var lowerSearch = searchTerm.ToLower();
                var count = await _context.Books
                    .Where(b =>
                        b.Title.ToLower().Contains(lowerSearch) ||
                        b.Author.ToLower().Contains(lowerSearch) ||
                        (b.Genres != null && b.Genres.ToLower().Contains(lowerSearch)))
                    .CountAsync();

                SetCache(cacheKey, count, TimeSpan.FromMinutes(15));
                return count;
            }
            catch
            {
                return 0;
            }
        }

        // CRUD OPERATIONS VỚI CACHE INVALIDATION
        public async Task<Book> AddBookAsync(Book book)
        {
            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            // Clear cache khi thêm sách mới
            ClearAllCaches();

            return book;
        }

        public async Task<Book> UpdateBookAsync(Book book)
        {
            _context.Entry(book).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Clear cache khi update
            ClearAllCaches();

            return book;
        }

        public async Task<bool> DeleteBookAsync(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null) return false;

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            // Clear cache khi xóa
            ClearAllCaches();

            return true;
        }

        // CACHE MANAGEMENT - SIMPLIFIED & SAFE
        public void ClearAllCaches()
        {
            try
            {
                lock (_cacheKeysLock)
                {
                    foreach (var key in _cacheKeys.ToList())
                    {
                        _cache.Remove(key);
                    }
                    _cacheKeys.Clear();
                }

                _logger.LogInformation($"🗑️ Cleared {_cacheKeys.Count} cache entries");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing caches");
            }
        }

        // SPECIFIC CACHE CLEARING
        public void ClearPageCaches()
        {
            try
            {
                lock (_cacheKeysLock)
                {
                    var pageCacheKeys = _cacheKeys.Where(k => k.StartsWith("books_page_") || k.StartsWith("top_rated_")).ToList();
                    foreach (var key in pageCacheKeys)
                    {
                        _cache.Remove(key);
                        _cacheKeys.Remove(key);
                    }
                }

                _logger.LogInformation("🗑️ Cleared page caches");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing page caches");
            }
        }

        public void ClearSearchCaches()
        {
            try
            {
                lock (_cacheKeysLock)
                {
                    var searchCacheKeys = _cacheKeys.Where(k => k.StartsWith("search_")).ToList();
                    foreach (var key in searchCacheKeys)
                    {
                        _cache.Remove(key);
                        _cacheKeys.Remove(key);
                    }
                }

                _logger.LogInformation("🗑️ Cleared search caches");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing search caches");
            }
        }

        // REMAINING INTERFACE IMPLEMENTATIONS (simplified)
        public async Task<IEnumerable<Book>> GetAllBooksAsync()
        {
            return await GetBooksPagedAsync(1, 1000);
        }

        public async Task<IEnumerable<Book>> SearchBooksByTitleAsync(string title)
        {
            return await SearchBooksPagedAsync(title, 1, 50);
        }

        public async Task<IEnumerable<Book>> GetBooksByGenreAsync(string genre)
        {
            var cacheKey = $"genre_{genre.ToLower()}_books";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<Book>? cachedBooks))
            {
                return cachedBooks!;
            }

            var books = await _context.Books
                .AsNoTracking()
                .Where(b => b.Genres != null && b.Genres.ToLower().Contains(genre.ToLower()))
                .Select(b => new Book
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    Price = b.Price,
                    CoverImg = b.CoverImg,
                    Rating = b.Rating
                })
                .OrderBy(b => b.Title)
                .Take(100)
                .ToListAsync();

            SetCache(cacheKey, books, TimeSpan.FromMinutes(30));
            return books;
        }

        public async Task<IEnumerable<Book>> GetBooksByAuthorAsync(string author)
        {
            var cacheKey = $"author_{author.ToLower()}_books";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<Book>? cachedBooks))
            {
                return cachedBooks!;
            }

            var books = await _context.Books
                .AsNoTracking()
                .Where(b => b.Author.ToLower().Contains(author.ToLower()))
                .Select(b => new Book
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    Price = b.Price,
                    CoverImg = b.CoverImg,
                    Rating = b.Rating
                })
                .OrderBy(b => b.Title)
                .Take(100)
                .ToListAsync();

            SetCache(cacheKey, books, TimeSpan.FromMinutes(30));
            return books;
        }

        public async Task<bool> IsConnectionHealthyAsync()
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SELECT 1");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<object> GetDatabaseInfoAsync()
        {
            try
            {
                var totalBooks = await GetTotalBooksCountAsync();
                var hasBooks = totalBooks > 0;

                return new
                {
                    HasBooks = hasBooks,
                    TotalBooks = totalBooks,
                    CacheEnabled = true,
                    DatabaseType = "Supabase PostgreSQL",
                    OptimizedForLargeDataset = true,
                    CachedKeysCount = _cacheKeys.Count
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    HasBooks = false,
                    TotalBooks = 0,
                    Error = ex.Message,
                    CacheEnabled = true
                };
            }
        }
    }
}