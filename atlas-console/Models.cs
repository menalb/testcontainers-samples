using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoAtlasTestContainer;

public class Person
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public ObjectId Id { get; set; }
    [BsonElement("name")]
    public required string Name { get; set; }
    [BsonElement("companyCatchPhrase")]
    public required string Department { get; set; }
    [BsonElement("embeddings")]
    public required float[] Embeddings { get; set; }
}
