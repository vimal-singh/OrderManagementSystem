using Microsoft.AspNetCore.Mvc;
using OrderManagementSystem.API.DTOs;
using OrderManagementSystem.API.Services;

namespace OrderManagementSystem.API.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController(IProductService productService) : ControllerBase
    {
        private readonly IProductService _productService = productService;

        [HttpGet]
        public async Task<IActionResult> GetAllProducts()
        {
            var products = await _productService.GetProductsAsync();
            return Ok(products);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get([FromRoute] int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return Ok(product);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductDTO productDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var createdProduct = await _productService.CreateProductAsync(productDto);
            return CreatedAtAction(nameof(Get), new { id = createdProduct.Id }, createdProduct);
        }
    }
}