using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace MiniShop.Order.API.Models
{
    public sealed class Order
    {
        [BsonRepresentation(BsonType.String)] // Guid olarak temsil edilir
        public Guid Id { get; private set; }
        public DateTime OrderDate { get; set; } // Sipariş tarihi
        public List<OrderItem> Items { get; set; } = new(); // Siparişteki ürünler
        public decimal TotalAmount { get; set; } // Toplam tutar

        public Order()
        {
            Id = Guid.NewGuid();
        }
    }

    public sealed class OrderItem
    {
        [BsonRepresentation(BsonType.String)] // Guid olarak temsil edilir
        public Guid Id { get; private set; }
        public string ProductId { get; set; } = null!; // Ürün ID (Product mikroservisinden alınacak)
        public int Quantity { get; set; } // Ürün miktarı
        public decimal Price { get; set; } // Ürün fiyatı

        public OrderItem()
        {
            Id = Guid.NewGuid();
        }
    }
}
