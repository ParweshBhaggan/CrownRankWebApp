using CrownRankApp.API.Authentication;
using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Services.Category;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrownRankApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController(ICategoryService service) : ControllerBase
    {
        [HttpGet]
        [EndpointSummary("Get all categories")]
        [EndpointDescription("Retrieves a list of all categories.")]
        [ProducesResponseType(typeof(List<CategoryResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CategoryResponseDto>>> GetCategories()
        {
            var categories = await service.GetAllAsync();
            return Ok(categories);
        }

        [HttpGet("{id:guid}")]
        [EndpointSummary("Get a category by ID")]
        [EndpointDescription("Retrieves a category by its unique identifier.")]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryResponseDto>> GetCategoryById(Guid id)
        {
            var category = await service.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound($"Category with id {id} not found.");
            }

            return Ok(category);
        }

        [HttpGet("name/{name}")]
        [EndpointSummary("Get a category by name")]
        [EndpointDescription("Retrieves a category by its name.")]
        [ProducesResponseType(typeof(CrownRankApp.Domain.Models.Category), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CrownRankApp.Domain.Models.Category>> GetCategoryByName(string name)
        {
            var category = await service.GetByNameAsync(name);
            if (category == null)
            {
                return NotFound($"Category with name '{name}' not found.");
            }

            return Ok(category);
        }

        [HttpPost]
        [Authorize(Roles = AdminRoles.Admin)]
        [EndpointSummary("Add a new category")]
        [EndpointDescription("Adds a new category to the system.")]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CategoryResponseDto>> AddCategory([FromBody] CategoryDto dto)
        {
            var category = await service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = AdminRoles.Admin)]
        [EndpointSummary("Update a category by ID")]
        [EndpointDescription("Updates an existing category by its unique identifier.")]
        [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryResponseDto>> UpdateCategory(Guid id, [FromBody] CategoryDto dto)
        {
            var existingCategory = await service.GetByIdAsync(id);
            if (existingCategory == null)
            {
                return NotFound($"Category with id {id} not found.");
            }

            var category = await service.UpdateAsync(id, dto);
            return Ok(category);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AdminRoles.Admin)]
        [EndpointSummary("Delete a category by ID")]
        [EndpointDescription("Deletes an existing category by its unique identifier.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteCategory(Guid id)
        {
            var deleted = await service.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
