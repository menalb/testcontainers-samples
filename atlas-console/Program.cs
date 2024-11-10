﻿using atlas_console;
using Bogus;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using SmartComponents.LocalEmbeddings;
using Testcontainers.MongoDb;

var username = "mongo_username";
var password = "mongo_password";
MongoDbContainer container = new MongoDbBuilder()
    .WithImage("mongodb/mongodb-atlas-local")
    .WithUsername(username)
    .WithPassword(password)
    .WithEnvironment("MONGODB_INITDB_ROOT_USERNAME", username)
    .WithEnvironment("MONGODB_INITDB_ROOT_PASSWORD", password)
    .Build();

await container.StartAsync();

var connectionString = container.GetConnectionString();
Console.WriteLine(container.GetConnectionString());

var urlBuilder = new MongoUrlBuilder(connectionString)
{
    DirectConnection = true
};

MongoClient mongoClient = new(urlBuilder.ToMongoUrl());
IMongoDatabase database = mongoClient.GetDatabase("test");
IMongoCollection<Student> collection = database.GetCollection<Student>("students");

using var embedder = new LocalEmbedder();
EmbeddingF32 GenerateEmbedding(string description)
{
    var s = $"### ### Description: {description}";
    return embedder.Embed(s);
}

// Create
var students = new Faker<Student>()
 .RuleFor(u => u.Name, (f, u) => f.Name.FirstName())
 .RuleFor(u => u.CompanyCatchPhrase, (f, u) => f.Person.Company.CatchPhrase)
 .RuleFor(u => u.Embeddings, (f, u) => GenerateEmbedding(u.CompanyCatchPhrase).Values.ToArray())
 .Generate(1000);

// Insert Students
await collection.InsertManyAsync(students);

Thread.Sleep(3000);

// Create Text Search
var command = new[]
       {
            "mongosh" ,
            "--username", username,
            "--password", password,
            "--quiet",
            "--eval",
            "db.students.createSearchIndex('student_name_index',{ mappings: { dynamic: true },'storedSource':{'include':['name']}} )"
        };

var result = await container.ExecAsync(command);

Console.WriteLine(result.Stdout);
Console.WriteLine(result.Stderr);

Thread.Sleep(3000);

// Create Vector Search
var commandVector = new[]
       {
            "mongosh" ,
            "--username", username,
            "--password", password,
            "--quiet",
            "--eval",
            "db.students.createSearchIndex('student_company_index','vectorSearch',{ fields: [{'numDimensions': 384, 'path': 'embeddings','similarity':'cosine','type':'vector'}]} )"
        };

var resultVector = await container.ExecAsync(commandVector);

Console.WriteLine(resultVector.Stdout);
Console.WriteLine(resultVector.Stderr);

Thread.Sleep(3000);

// Read
var filterBuilder = Builders<Student>.Filter;
var filter = filterBuilder.Eq("Name", students.First().Name);
var results = await collection.Find(filter).ToListAsync();

results.ForEach(x => Console.WriteLine(x.Name));

// Update
var updateBuilder = Builders<Student>.Update;
var update = updateBuilder.Set("Name", "Dev Leader");
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
      Builders<Student>.Search.Text(x => x.Name, "Dev", fuzzyOptions),
      indexName: "student_name_index"
    );

Thread.Sleep(1000);

var devs = await agg.ToListAsync();
devs.ForEach(x => Console.WriteLine(x.Name));

Console.WriteLine("");

// Vector
Console.WriteLine("Vector Search");
var options = new VectorSearchOptions<Student>()
{
    IndexName = "student_company_index",
    NumberOfCandidates = 150,
};

var target = embedder.Embed("music");

var aggVector = collection
.Aggregate()
.VectorSearch(m => m.Embeddings, target.Values, 10, options);

var v = await aggVector.ToListAsync();
v.ForEach(x => Console.WriteLine($"{x.Name} - {x.CompanyCatchPhrase}"));


// Delete
filter = filterBuilder.Eq("Name", "Dev Leader");
collection.DeleteOne(filter);

await container.StopAsync();
