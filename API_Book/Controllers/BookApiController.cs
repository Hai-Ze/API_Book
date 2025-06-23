using API_Book.Models;
using API_Book.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BookApiController : ControllerBase
{
    private readonly IBookRepository _bookRepository;

    public BookApiController(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create new book")]
    public async Task<ActionResult<Book>> CreateBook([FromBody] Book book)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var createdBook = await _bookRepository.AddBookAsync(book);
            return CreatedAtAction(nameof(GetBookById), new { id = createdBook.Id }, createdBook);
        }
        catch (DbUpdateException ex)
        {
            var innerExceptionMessage = ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"DbUpdateException: {innerExceptionMessage}");
            return StatusCode(500, $"Internal server error: {innerExceptionMessage}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General Exception: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("paged")]
    [SwaggerOperation(
        Summary = "Get books with true server-side pagination",
        Description = "Chỉ load đúng số sách cần thiết cho trang hiện tại, không load hết database"
    )]
    public async Task<ActionResult> GetBooksPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 15)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;
            if (pageSize > 100) pageSize = 100;

            var startTime = DateTime.UtcNow;
            var books = await _bookRepository.GetBooksPagedAsync(page, pageSize);
            var totalBooks = await _bookRepository.GetTotalBooksCountAsync();

            var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var totalPages = totalBooks > 0 ? (int)Math.Ceiling((double)totalBooks / pageSize) : 1;

            var result = new
            {
                Data = books,
                Page = page,
                PageSize = pageSize,
                TotalBooks = totalBooks,
                TotalPages = totalPages,
                HasPrevious = page > 1,
                HasNext = page < totalPages,
                LoadTimeMs = Math.Round(loadTime, 2),
                IsPaginated = true,
                Message = $"Loaded {books.Count()} books in {loadTime:F0}ms"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Error = "Internal server error",
                Message = ex.Message,
                IsPaginated = true
            });
        }
    }

    // Bỏ [Authorize] để test
    [HttpGet("{id}/details")]
    [SwaggerOperation(Summary = "Get detailed book information including ratings")]
    // [Authorize] // Comment hoặc xóa tạm thời
    public async Task<ActionResult<Book>> GetBookDetails(int id)
    {
        try
        {
            var book = await _bookRepository.GetBookByIdAsync(id);

            if (book == null)
            {
                return NotFound($"Book with ID {id} not found.");
            }

            return Ok(book);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("search")]
    [SwaggerOperation(Summary = "Search books with true server-side pagination")]
    public async Task<ActionResult> SearchBooks(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query is required");
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;
            if (pageSize > 100) pageSize = 100;

            var startTime = DateTime.UtcNow;
            var books = await _bookRepository.SearchBooksPagedAsync(q.Trim(), page, pageSize);
            var totalBooks = await _bookRepository.GetSearchResultsCountAsync(q.Trim());

            var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var totalPages = totalBooks > 0 ? (int)Math.Ceiling((double)totalBooks / pageSize) : 1;

            var result = new
            {
                Data = books,
                Page = page,
                PageSize = pageSize,
                TotalBooks = totalBooks,
                TotalPages = totalPages,
                HasPrevious = page > 1,
                HasNext = page < totalPages,
                SearchQuery = q.Trim(),
                LoadTimeMs = Math.Round(loadTime, 2),
                Message = $"Found {books.Count()} books for '{q}' in {loadTime:F0}ms"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Error = "Search error",
                Message = ex.Message,
                SearchQuery = q
            });
        }
    }

    [HttpGet("top-rated")]
    [SwaggerOperation(Summary = "Get top rated books with true pagination")]
    public async Task<ActionResult> GetTopRatedBooks([FromQuery] int page = 1, [FromQuery] int pageSize = 15)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 15;
            if (pageSize > 100) pageSize = 100;

            var startTime = DateTime.UtcNow;
            var books = await _bookRepository.GetTopRatedBooksPagedAsync(page, pageSize);
            var totalBooks = await _bookRepository.GetTotalBooksCountAsync();

            var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var totalPages = totalBooks > 0 ? (int)Math.Ceiling((double)totalBooks / pageSize) : 1;

            var result = new
            {
                Data = books,
                Page = page,
                PageSize = pageSize,
                TotalBooks = totalBooks,
                TotalPages = totalPages,
                HasPrevious = page > 1,
                HasNext = page < totalPages,
                LoadTimeMs = Math.Round(loadTime, 2),
                Type = "TopRated"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Error = "Top rated books error",
                Message = ex.Message
            });
        }
    }

    [HttpGet]
    [SwaggerOperation(Summary = "Get all books (limited for compatibility)")]
    public async Task<ActionResult<IEnumerable<Book>>> GetAllBooks()
    {
        try
        {
            var books = await _bookRepository.GetAllBooksAsync();
            return Ok(books);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get book by ID")]
    public async Task<ActionResult<Book>> GetBookById(int id)
    {
        try
        {
            var book = await _bookRepository.GetBookByIdAsync(id);

            if (book == null)
            {
                return NotFound($"Book with ID {id} not found.");
            }

            return Ok(book);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Update book")]
    public async Task<ActionResult<Book>> UpdateBook(int id, [FromBody] Book book)
    {
        try
        {
            if (id != book.Id)
            {
                return BadRequest("ID in URL does not match ID in request body.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingBook = await _bookRepository.GetBookByIdAsync(id);
            if (existingBook == null)
            {
                return NotFound($"Book with ID {id} not found.");
            }

            var updatedBook = await _bookRepository.UpdateBookAsync(book);
            return Ok(updatedBook);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete book")]
    public async Task<ActionResult> DeleteBook(int id)
    {
        try
        {
            var result = await _bookRepository.DeleteBookAsync(id);

            if (!result)
            {
                return NotFound($"Book with ID {id} not found.");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("quick-stats")]
    [SwaggerOperation(Summary = "Get quick statistics with sample data")]
    public async Task<ActionResult> GetQuickStats()
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var recentBooks = await _bookRepository.GetBooksPagedAsync(1, 5);
            var topRatedBooks = await _bookRepository.GetTopRatedBooksPagedAsync(1, 5);
            var totalBooks = await _bookRepository.GetTotalBooksCountAsync();

            var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var result = new
            {
                TotalBooks = totalBooks,
                RecentBooks = recentBooks,
                TopRatedBooks = topRatedBooks,
                LoadTimeMs = Math.Round(loadTime, 2),
                IsPaginated = true,
                Note = "Sample data for dashboard"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Error = "Quick stats error",
                Message = ex.Message
            });
        }
    }
}