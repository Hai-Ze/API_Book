using API_Book.Models;
using API_Book.Repositories;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace API_Book.Controllers
{
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

        // CÁC METHODS CŨ GIỮ NGUYÊN...

        [HttpGet]
        [SwaggerOperation(Summary = "Get all books")]
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

        // THÊM METHOD MỚI CHO PHÂN TRANG
        /// <summary>
        /// Get books with pagination
        /// </summary>
        /// <param name="page">Page number (starting from 1)</param>
        /// <param name="pageSize">Number of books per page</param>
        /// <returns>Paginated books</returns>
        [HttpGet("paged")]
        [SwaggerOperation(Summary = "Get books with pagination", Description = "Retrieve books with pagination support")]
        [SwaggerResponse(200, "Success")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<ActionResult> GetBooksPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100; // Limit max page size

                var books = await _bookRepository.GetBooksPagedAsync(page, pageSize);
                var totalBooks = await _bookRepository.GetTotalBooksCountAsync();
                var totalPages = (int)Math.Ceiling((double)totalBooks / pageSize);

                var result = new
                {
                    Data = books,
                    Page = page,
                    PageSize = pageSize,
                    TotalBooks = totalBooks,
                    TotalPages = totalPages,
                    HasPrevious = page > 1,
                    HasNext = page < totalPages
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}