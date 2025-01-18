using Microsoft.Extensions.Options;
using MiniShop.Order.API.Options;
using MongoDB.Driver;

namespace MiniShop.Order.API.Context;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDBSettings> mongoSettings)
    {
        // MongoDB ayarlarını al ve veritabanını bağla
        var settings = mongoSettings.Value;

        var mongoClient = new MongoClient(settings.ConnectionString);

        _database = mongoClient.GetDatabase(settings.DatabaseName);
    }

    // Generic koleksiyon erişimi
    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        return _database.GetCollection<T>(collectionName);
    }
}

