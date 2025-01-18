namespace MiniShop.Order.API.Dtos;

public record GetAllOrderDto(Guid Id, DateTime OrderDate, ICollection<GetOrderItemDto> Items, decimal TotalAmount);

public record GetOrderItemDto(string ProductId, string ProductName, int Quantity, decimal Price);
