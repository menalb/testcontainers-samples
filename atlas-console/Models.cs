// See https://aka.ms/new-console-template for more information
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace atlas_console;

public class Student
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public ObjectId Id { get; set; }
    [BsonElement("name")]
    public required string Name { get; set; }
    [BsonElement("companyCatchPhrase")]
    public required string CompanyCatchPhrase { get; set; }
    [BsonElement("embeddings")]
    public required float[] Embeddings { get; set; }
}
