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

        // Phân trang cơ bản
        Task<IEnumerable<Book>> GetBooksPagedAsync(int page, int pageSize);
        Task<int> GetTotalBooksCountAsync();

        // Phân trang nâng cao
        Task<IEnumerable<Book>> GetTopRatedBooksPagedAsync(int page, int pageSize);
        Task<IEnumerable<Book>> SearchBooksPagedAsync(string searchTerm, int page, int pageSize);
        Task<int> GetSearchResultsCountAsync(string searchTerm);

        // Utility methods
        Task<bool> IsConnectionHealthyAsync();
        Task<object> GetDatabaseInfoAsync();
    }
}