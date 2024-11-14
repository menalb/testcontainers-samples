using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace MongoAtlasTestContainer;

public class MongoIndexHelper
{
    public static MongoIndexHelper BuildContainer(string username, string password)
    {
        return new(new MongoDbBuilder()
            .WithImage("mongodb/mongodb-atlas-local")
            .WithUsername(username)
            .WithPassword(password)
            .WithEnvironment("MONGODB_INITDB_ROOT_USERNAME", username)
            .WithEnvironment("MONGODB_INITDB_ROOT_PASSWORD", password)
            .Build(),
            username,
            password
            );
    }

    private readonly string Username;
    private readonly string Password;
    private MongoIndexHelper(MongoDbContainer container, string username, string password)
    {
        Container = container;
        Username = username;
        Password = password;
    }

    public readonly MongoDbContainer Container;

    public async Task CreateSearchIndex<T>(string indexFilePath, string indexName, IMongoCollection<T> collection)
    {
        var indexScript = File.ReadAllText(indexFilePath);
        var name = collection.CollectionNamespace.CollectionName;
        var command = new[]
        {
            "mongosh" ,
            "--username", Username,
            "--password", Password,
            "--quiet",
            "--eval",
            $"db.{name}.createSearchIndex('{indexName}',{indexScript})"
        };
        var result = await Container.ExecAsync(command);

        Console.WriteLine(result.Stdout);
        Console.WriteLine(result.Stderr);

        await IndexExists(collection, indexName);
    }

    public async Task CreateVectorIndex<T>(string indexFilePath, string indexName, IMongoCollection<T> collection)
    {
        var indexScript = File.ReadAllText(indexFilePath);
        var name = collection.CollectionNamespace.CollectionName;
        var command = new[]
        {
            "mongosh" ,
            "--username", Username,
            "--password", Password,
            "--quiet",
            "--eval",
            $"db.{name}.createSearchIndex('{indexName}','vectorSearch',{indexScript})"
        };
        var result = await Container.ExecAsync(command);

        Console.WriteLine(result.Stdout);
        Console.WriteLine(result.Stderr);

        await IndexExists(collection, indexName);
    }

    private static T? TryGetValue<T>(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out var value))
        {
            return default;
        }

        var result = BsonTypeMapper.MapToDotNetValue(value);
        return (T)result;
    }


    private async static Task IndexExists<T>(IMongoCollection<T> collection, string indexName)
    {
        var exit = false;
        var i = 0;
        while (!exit && i < 60)
        {
            Thread.Sleep(500);
            var ind = await collection.SearchIndexes.ListAsync(indexName);
            var first = await ind.FirstOrDefaultAsync();
            var s = TryGetValue<string>(first, "status");
            Console.WriteLine(s);

            exit = s == "READY";

            i++;
        }
    }

}
