using MiniShop.Order.API.Context;
using MiniShop.Order.API.Dtos;
using MiniShop.Order.API.Models;
using MiniShop.Order.API.Options;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// MongoDB ayarlarýný baðla
builder.Services.Configure<MongoDBSettings>(builder.Configuration.GetSection("MongoDBSettings"));

// MongoDB context DI
builder.Services.AddSingleton<MongoDbContext>();

builder.Services.AddHttpClient("ProductService", client =>
{
    var productServiceBaseAddress = builder.Configuration["ProductService:BaseAddress"];
    client.BaseAddress = new Uri(productServiceBaseAddress!);
});

var app = builder.Build();

// CreateOrder POST endpoint to handle multiple orders
app.MapPost("/create-order", async (List<CreateOrderDto> request, MongoDbContext context, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    // MongoDB koleksiyonunu al
    var orderCollection = context.GetCollection<Order>("Orders");

    // Ürün servisi için HttpClient oluþtur
    var httpClient = httpClientFactory.CreateClient("ProductService");

    var createdOrders = new List<Order>();

    foreach (var createOrderDto in request)
    {
        // Sipariþin ürün bilgilerini ürün servisinden al
        var orderItems = new List<OrderItem>();

        foreach (var item in createOrderDto.Items)
        {
            var response = await httpClient.GetAsync($"/api/products/{item.ProductId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var product = await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken: cancellationToken);

                if (product != null)
                {
                    orderItems.Add(new OrderItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = product.Price
                    });
                }
            }
            else
            {
                // Ürün servisi cevap veremezse varsayýlan fiyatý kullan
                orderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                });
            }
        }

        // Sipariþ oluþtur
        var newOrder = new Order
        {
            OrderDate = DateTime.UtcNow,
            Items = orderItems,
            TotalAmount = orderItems.Sum(i => i.Quantity * i.Price)
        };

        createdOrders.Add(newOrder);
    }

    // MongoDB'ye birden fazla sipariþi ayný anda kaydet
    if (createdOrders.Any())
    {
        await orderCollection.InsertManyAsync(createdOrders, cancellationToken: cancellationToken);
    }

    // Oluþturulan sipariþleri döndür
    return Results.Created("/orders", createdOrders.Select(o => new
    {
        o.Id,
        o.OrderDate,
        o.TotalAmount,
        Items = o.Items.Select(i => new
        {
            i.ProductId,
            i.Quantity,
            i.Price
        })
    }));
});

// GetAllOrders GET endpoint
app.MapGet("/orders", async (MongoDbContext context, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    // Orders koleksiyonunu al
    var orderCollection = context.GetCollection<Order>("Orders");
    var httpClient = httpClientFactory.CreateClient("ProductService");

    // Tüm sipariþleri getir
    var orders = await orderCollection.Find(_ => true).ToListAsync(cancellationToken);

    // Sipariþlerdeki ürün bilgilerini doldur
    var getAllOrderDtos = new List<GetAllOrderDto>();

    foreach (var order in orders)
    {
        var orderItems = new List<GetOrderItemDto>();

        foreach (var item in order.Items)
        {
            // Ürün bilgilerini ürün mikroservisinden al
            var response = await httpClient.GetAsync($"/api/products/{item.ProductId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var product = await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken: cancellationToken);

                if (product != null)
                {
                    var orderItem = new GetOrderItemDto(
                        item.ProductId,
                        product.Name,
                        item.Quantity,
                        product.Price
                    );
                    orderItems.Add(orderItem);
                }
            }
            else
            {
                // Eðer ürün bilgisi alýnamazsa fallback mekanizmasý (ör. item.Price kullanýmý)
                var fallbackOrderItem = new GetOrderItemDto(
                    item.ProductId,
                    "Unknown Product",
                    item.Quantity,
                    item.Price
                );
                orderItems.Add(fallbackOrderItem);
            }
        }

        // Sipariþ DTO'sunu oluþtur
        var orderDto = new GetAllOrderDto(
            order.Id,
            order.OrderDate,
            orderItems,
            order.TotalAmount
        );
        getAllOrderDtos.Add(orderDto);
    }

    return Results.Ok(getAllOrderDtos);
});


app.MapGet("/", () => "Hello World!");

app.Run();
