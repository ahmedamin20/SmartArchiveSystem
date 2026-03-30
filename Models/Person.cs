using System;

namespace SmartArchive.Models
{
    public class Person
    {
        public int Id { get; set; }
        public required string NationalId { get; set; }
        public required string FullName { get; set; }
        public required string FolderPath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
