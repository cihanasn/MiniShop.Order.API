namespace MiniShop.Order.API.Dtos;

public record CreateOrderDto(ICollection<CreateOrderItemDto> Items);

public record CreateOrderItemDto(string ProductId, int Quantity, decimal Price);
