using MiniShop.Order.API.Context;
using MiniShop.Order.API.Dtos;
using MiniShop.Order.API.Models;
using MiniShop.Order.API.Options;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// MongoDB ayarlar�n� ba�la
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

    // �r�n servisi i�in HttpClient olu�tur
    var httpClient = httpClientFactory.CreateClient("ProductService");

    var createdOrders = new List<Order>();

    foreach (var createOrderDto in request)
    {
        // Sipari�in �r�n bilgilerini �r�n servisinden al
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
                // �r�n servisi cevap veremezse varsay�lan fiyat� kullan
                orderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                });
            }
        }

        // Sipari� olu�tur
        var newOrder = new Order
        {
            OrderDate = DateTime.UtcNow,
            Items = orderItems,
            TotalAmount = orderItems.Sum(i => i.Quantity * i.Price)
        };

        createdOrders.Add(newOrder);
    }

    // MongoDB'ye birden fazla sipari�i ayn� anda kaydet
    if (createdOrders.Any())
    {
        await orderCollection.InsertManyAsync(createdOrders, cancellationToken: cancellationToken);
    }

    // Olu�turulan sipari�leri d�nd�r
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

    // T�m sipari�leri getir
    var orders = await orderCollection.Find(_ => true).ToListAsync(cancellationToken);

    // Sipari�lerdeki �r�n bilgilerini doldur
    var getAllOrderDtos = new List<GetAllOrderDto>();

    foreach (var order in orders)
    {
        var orderItems = new List<GetOrderItemDto>();

        foreach (var item in order.Items)
        {
            // �r�n bilgilerini �r�n mikroservisinden al
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
                // E�er �r�n bilgisi al�namazsa fallback mekanizmas� (�r. item.Price kullan�m�)
                var fallbackOrderItem = new GetOrderItemDto(
                    item.ProductId,
                    "Unknown Product",
                    item.Quantity,
                    item.Price
                );
                orderItems.Add(fallbackOrderItem);
            }
        }

        // Sipari� DTO'sunu olu�tur
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
