using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Api.Data;
using Shop.Api.Dtos;
using Shop.Api.Models;

namespace Shop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly ShopDbContext _db;

    public CartController(ShopDbContext db) => _db = db;

    // MVP: single global cart — replace with per-user lookup once auth is in place.
    private async Task<Cart> GetOrCreateCartAsync()
    {
        var cart = await _db.Carts
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync();

        if (cart is not null)
            return cart;

        cart = new Cart();
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();

        return await _db.Carts
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .FirstAsync();
    }

    // GET /api/cart
    [HttpGet]
    public async Task<ActionResult<Cart>> Get() =>
        Ok(await GetOrCreateCartAsync());

    // POST /api/cart/items
    [HttpPost("items")]
    public async Task<ActionResult<Cart>> AddItem(AddCartItemDto dto)
    {
        var product = await _db.Products.FindAsync(dto.ProductId);
        if (product is null)
            return NotFound("Product not found.");

        var cart = await GetOrCreateCartAsync();
        var existing = cart.Items.FirstOrDefault(i => i.ProductId == dto.ProductId);

        if (existing is not null)
            existing.Quantity += dto.Quantity;
        else
            cart.Items.Add(new CartItem { CartId = cart.Id, ProductId = dto.ProductId, Quantity = dto.Quantity });

        await _db.SaveChangesAsync();
        return Ok(await GetOrCreateCartAsync());
    }

    // PUT /api/cart/items/{id}
    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(int id, UpdateCartItemDto dto)
    {
        var item = await _db.CartItems.FindAsync(id);
        if (item is null)
            return NotFound();

        if (dto.Quantity <= 0)
            _db.CartItems.Remove(item);
        else
            item.Quantity = dto.Quantity;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/cart/items/{id}
    [HttpDelete("items/{id}")]
    public async Task<IActionResult> RemoveItem(int id)
    {
        var item = await _db.CartItems.FindAsync(id);
        if (item is null)
            return NotFound();

        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
