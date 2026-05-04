using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Api.Data;
using Shop.Api.Dtos;
using Shop.Api.Models;

namespace Shop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ShopDbContext _db;

    public OrdersController(ShopDbContext db) => _db = db;

    // GET /api/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetAll() =>
        await _db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

    // GET /api/orders/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetById(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order is null ? NotFound() : Ok(order);
    }

    // POST /api/orders — creates an order from the current cart then clears it.
    [HttpPost]
    public async Task<ActionResult<Order>> Create(CreateOrderDto _)
    {
        var cart = await _db.Carts
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync();

        if (cart is null || cart.Items.Count == 0)
            return BadRequest("Cart is empty.");

        // Validate products exist and capture prices at checkout time.
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var missing = productIds.Except(products.Keys).ToList();
        if (missing.Count > 0)
            return BadRequest($"Products not found: {string.Join(", ", missing)}.");

        var orderItems = cart.Items.Select(i => new OrderItem
        {
            ProductId   = i.ProductId,
            ProductName = products[i.ProductId].Name,
            UnitPrice   = products[i.ProductId].Price,
            Quantity    = i.Quantity
        }).ToList();

        var order = new Order
        {
            CreatedAtUtc = DateTime.UtcNow,
            Total        = orderItems.Sum(i => i.UnitPrice * i.Quantity),
            Items        = orderItems
        };

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cart.Items);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}
