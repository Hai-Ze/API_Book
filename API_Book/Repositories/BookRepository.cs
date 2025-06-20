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

        public async Task<IEnumerable<Book>> GetAllBooksAsync()
        {
            return await _context.Books
                .AsNoTracking()
                .OrderBy(b => b.Id)
                .ToListAsync();
        }

        public async Task<Book?> GetBookByIdAsync(int id)
        {
            return await _context.Books
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);
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
                .ToListAsync();
        }

        public async Task<IEnumerable<Book>> GetBooksByGenreAsync(string genre)
        {
            return await _context.Books
                .AsNoTracking()
                .Where(b => b.Genres != null && b.Genres.ToLower().Contains(genre.ToLower()))
                .OrderBy(b => b.Title)
                .ToListAsync();
        }

        public async Task<IEnumerable<Book>> GetBooksByAuthorAsync(string author)
        {
            return await _context.Books
                .AsNoTracking()
                .Where(b => b.Author.ToLower().Contains(author.ToLower()))
                .OrderBy(b => b.Title)
                .ToListAsync();
        }

        // THÊM 2 METHODS MỚI CHO PHÂN TRANG
        public async Task<IEnumerable<Book>> GetBooksPagedAsync(int page, int pageSize)
        {
            var skip = (page - 1) * pageSize;

            return await _context.Books
                .AsNoTracking()
                .OrderBy(b => b.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalBooksCountAsync()
        {
            return await _context.Books.CountAsync();
        }
    }
}