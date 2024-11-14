using MongoAtlasTestContainer;
using Bogus;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using SmartComponents.LocalEmbeddings;

var username = "mongo_username";
var password = "mongo_password";

var mongoIndex = MongoIndexHelper.BuildContainer(username, password);

var container = mongoIndex.Container;

await container.StartAsync();

var connectionString = container.GetConnectionString();
Console.WriteLine(container.GetConnectionString());

var urlBuilder = new MongoUrlBuilder(connectionString)
{
    DirectConnection = true
};

MongoClient mongoClient = new(urlBuilder.ToMongoUrl());
IMongoDatabase database = mongoClient.GetDatabase("test");
IMongoCollection<MongoAtlasTestContainer.Person> collection = database.GetCollection<MongoAtlasTestContainer.Person>("employees");

using var embedder = new LocalEmbedder();
EmbeddingF32 GenerateEmbedding(string description)
{
    var s = $"### Description: {description}";
    return embedder.Embed(s);
}

// Create
var people = new Faker<MongoAtlasTestContainer.Person>()
 .RuleFor(u => u.Name, (f, u) => f.Person.FirstName)
 .RuleFor(u => u.Department, (f, u) => f.Commerce.Department())
 .RuleFor(u => u.Embeddings, (f, u) => GenerateEmbedding(u.Department).Values.ToArray())
 .Generate(1000);

try
{
    // Insert
    using (var session = await mongoClient.StartSessionAsync())
    {
        // Begin transaction
        session.StartTransaction();
        try
        {
            await collection.InsertManyAsync(people);
            await session.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            Console.WriteLine(ex.Message);
        }
    }


    // Create Text Search
    var textSearchIndex = "name_index";
    await mongoIndex.CreateSearchIndex(@"indexes\text.json", textSearchIndex, collection);

    // Create Vector Search
    var vectorSearchIndex = "department_index";
    await mongoIndex.CreateVectorIndex(@"indexes\vector.json", vectorSearchIndex, collection);

    // Read
    var filterBuilder = Builders<MongoAtlasTestContainer.Person>.Filter;
    var filter = filterBuilder.Eq("Name", people.First().Name);
    var results = await collection.Find(filter).ToListAsync();

    results.ForEach(x => Console.WriteLine(x.Name));

    // Update
    var updateBuilder = Builders<MongoAtlasTestContainer.Person>.Update;
    var update = updateBuilder.Set("Name", "Dev Test");
    collection.UpdateOne(filter, update);

    // Search
    Console.WriteLine("Text Search");
    var fuzzyOptions = new SearchFuzzyOptions()
    {
        MaxEdits = 1,
        PrefixLength = 1,
        MaxExpansions = 256
    };

    var agg = collection
        .Aggregate()
        .Search(
          Builders<MongoAtlasTestContainer.Person>.Search.Text(x => x.Name, "Dev", fuzzyOptions),
          indexName: textSearchIndex
        );

    var devs = await agg.ToListAsync();
    devs.ForEach(x => Console.WriteLine(x.Name));

    Console.WriteLine("");

    // Vector
    Console.WriteLine("Vector Search");
    var options = new VectorSearchOptions<MongoAtlasTestContainer.Person>()
    {
        IndexName = vectorSearchIndex,
        NumberOfCandidates = 150,
    };

    var target = embedder.Embed("music");

    var aggVector = collection
    .Aggregate()
    .VectorSearch(m => m.Embeddings, target.Values, 10, options);

    var v = await aggVector.ToListAsync();
    v.ForEach(x => Console.WriteLine($"{x.Name} - {x.Department}"));


    // Delete
    filter = filterBuilder.Eq("Name", "Dev Leader");
    collection.DeleteOne(filter);

}
catch
{
    throw;
}
finally
{
    mongoClient.Dispose();
    await container.StopAsync();
    await container.DisposeAsync();
}
