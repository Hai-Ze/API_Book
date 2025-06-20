using API_Book.Models;

namespace API_Book.Repositories
{
    public interface IBookRepository
    {
        Task<IEnumerable<Book>> GetAllBooksAsync();
        Task<Book?> GetBookByIdAsync(int id);
        Task<Book> AddBookAsync(Book book);
        Task<Book> UpdateBookAsync(Book book);
        Task<bool> DeleteBookAsync(int id);
        Task<IEnumerable<Book>> SearchBooksByTitleAsync(string title);
        Task<IEnumerable<Book>> GetBooksByGenreAsync(string genre);
        Task<IEnumerable<Book>> GetBooksByAuthorAsync(string author);

        // Thêm method phân trang
        Task<IEnumerable<Book>> GetBooksPagedAsync(int page, int pageSize);
        Task<int> GetTotalBooksCountAsync();
    }
}