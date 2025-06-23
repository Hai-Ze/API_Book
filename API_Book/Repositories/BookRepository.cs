using API_Book.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Book.Repositories
{
    public class BookRepository : IBookRepository
    {
        private readonly ApplicationDbContext _context;

        public BookRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // TRUE PAGINATION - CHỈ LẤY ĐÚNG SỐ LƯỢNG CẦN THIẾT
        public async Task<IEnumerable<Book>> GetBooksPagedAsync(int page, int pageSize)
        {
            var skip = (page - 1) * pageSize;

            return await _context.Books
                .AsNoTracking()
                .OrderBy(b => b.Id)  // Đảm bảo thứ tự consistent
                .Skip(skip)          // Bỏ qua số record trước đó
                .Take(pageSize)      // CHỈ LẤY ĐÚNG SỐ LƯỢNG CẦN THIẾT
                .Select(b => new Book
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    Price = b.Price,
                    CoverImg = b.CoverImg,
                    RatingsByStars = b.RatingsByStars // Thêm trường ratingsByStars
                })
                .ToListAsync();
        }

        // ĐẾM TỔNG SÁCH - KHÔNG CÓ GIỚI HẠN
        public async Task<int> GetTotalBooksCountAsync()
        {
            try
            {
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return await _context.Books.CountAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Nếu timeout, thử ước tính
                Console.WriteLine("Count query timeout, estimating...");
                return 30000; // Ước tính dựa trên kinh nghiệm
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error counting books: {ex.Message}");
                return 0;
            }
        }

        // SEARCH VỚI TRUE PAGINATION
        public async Task<IEnumerable<Book>> SearchBooksPagedAsync(string searchTerm, int page, int pageSize)
        {
            var skip = (page - 1) * pageSize;
            var lowerSearchTerm = searchTerm.ToLower();

            return await _context.Books
                .AsNoTracking()
                .Where(b =>
                    b.Title.ToLower().Contains(lowerSearchTerm) ||
                    b.Author.ToLower().Contains(lowerSearchTerm) ||
                    (b.Genres != null && b.Genres.ToLower().Contains(lowerSearchTerm)))
                .OrderBy(b => b.Title)
                .Skip(skip)      // Chỉ bỏ qua
                .Take(pageSize)  // Chỉ lấy đúng số cần
                .Select(b => new Book
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    Price = b.Price,
                    CoverImg = b.CoverImg,
                    RatingsByStars = b.RatingsByStars // Thêm trường ratingsByStars
                })
                .ToListAsync();
        }

        public async Task<int> GetSearchResultsCountAsync(string searchTerm)
        {
            try
            {
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var lowerSearchTerm = searchTerm.ToLower();

                return await _context.Books
                    .Where(b =>
                        b.Title.ToLower().Contains(lowerSearchTerm) ||
                        b.Author.ToLower().Contains(lowerSearchTerm) ||
                        (b.Genres != null && b.Genres.ToLower().Contains(lowerSearchTerm)))
                    .CountAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return 100; // Ước tính nếu timeout
            }
        }

        // TOP RATED VỚI TRUE PAGINATION
        public async Task<IEnumerable<Book>> GetTopRatedBooksPagedAsync(int page, int pageSize)
        {
            var skip = (page - 1) * pageSize;

            return await _context.Books
                .AsNoTracking()
                .Where(b => b.Rating.HasValue && b.Rating.Value > 0)
                .OrderByDescending(b => b.Rating)
                .ThenBy(b => b.Id)
                .Skip(skip)
                .Take(pageSize)
                .Select(b => new Book
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    Price = b.Price,
                    CoverImg = b.CoverImg,
                    RatingsByStars = b.RatingsByStars // Thêm trường ratingsByStars
                })
                .ToListAsync();
        }

        // CÁC METHODS CŨ - KHÔNG THAY ĐỔI
        public async Task<IEnumerable<Book>> GetAllBooksAsync()
        {
            // Chỉ dùng cho export hoặc admin view
            return await _context.Books
                .AsNoTracking()
                .OrderBy(b => b.Id)
                .Take(1000) // Vẫn giới hạn để tránh memory issues
                .ToListAsync();
        }

        public async Task<Book?> GetBookByIdAsync(int id)
        {
            return await _context.Books
                .AsNoTracking()
                .Where(b => b.Id == id)
                .Select(b => new Book
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    Price = b.Price,
                    CoverImg = b.CoverImg,
                    Description = b.Description,
                    Genres = b.Genres,
                    Rating = b.Rating,
                    NumRatings = b.NumRatings,
                    Pages = b.Pages,
                    Language = b.Language,
                    Series = b.Series,
                    RatingsByStars = b.RatingsByStars
                })
                .FirstOrDefaultAsync();
        }

        public async Task<Book> AddBookAsync(Book book)
        {
            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            return book;
        }

        public async Task<Book> UpdateBookAsync(Book book)
        {
            _context.Entry(book).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return book;
        }

        public async Task<bool> DeleteBookAsync(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
                return false;

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Book>> SearchBooksByTitleAsync(string title)
        {
            return await _context.Books
                .AsNoTracking()
                .Where(b => b.Title.ToLower().Contains(title.ToLower()))
                .OrderBy(b => b.Title)
                .Take(100) // Giới hạn cho old methods
                .ToListAsync();
        }

        public async Task<IEnumerable<Book>> GetBooksByGenreAsync(string genre)
        {
            return await _context.Books
                .AsNoTracking()
                .Where(b => b.Genres != null && b.Genres.ToLower().Contains(genre.ToLower()))
                .OrderBy(b => b.Title)
                .Take(100)
                .ToListAsync();
        }

        public async Task<IEnumerable<Book>> GetBooksByAuthorAsync(string author)
        {
            return await _context.Books
                .AsNoTracking()
                .Where(b => b.Author.ToLower().Contains(author.ToLower()))
                .OrderBy(b => b.Title)
                .Take(100)
                .ToListAsync();
        }

        public async Task<bool> IsConnectionHealthyAsync()
        {
            try
            {
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationTokenSource.Token);
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
                var firstBook = await _context.Books
                    .AsNoTracking()
                    .OrderBy(b => b.Id)
                    .FirstOrDefaultAsync();

                var lastBook = await _context.Books
                    .AsNoTracking()
                    .OrderByDescending(b => b.Id)
                    .FirstOrDefaultAsync();

                return new
                {
                    HasBooks = firstBook != null,
                    FirstBookId = firstBook?.Id,
                    LastBookId = lastBook?.Id,
                    IsPaginated = true,
                    Note = "Using true server-side pagination"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting database info: {ex.Message}");
                return new
                {
                    HasBooks = false,
                    FirstBookId = (int?)null,
                    LastBookId = (int?)null,
                    IsPaginated = true,
                    Error = ex.Message
                };
            }
        }
    }
}