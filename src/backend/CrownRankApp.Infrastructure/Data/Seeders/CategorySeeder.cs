using CrownRankApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Infrastructure.Data.Seeders
{
    public static class CategorySeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>().HasData(
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                    Name = "Streamer",
                    Description = "Live-streaming creators and personalities.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                    Name = "Gaming",
                    Description = "Gaming creators, players and esports personalities.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                    Name = "Influencer",
                    Description = "Lifestyle and social-media personalities.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
                    Name = "Adult Entertainment",
                    Description = "Adult-oriented creators and performers.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
                    Name = "Beauty & Fashion",
                    Description = "Beauty, style and fashion creators.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000006"),
                    Name = "Fitness & Wellness",
                    Description = "Fitness, health and wellness creators.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000007"),
                    Name = "Music",
                    Description = "Musicians, singers, producers and DJs.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000008"),
                    Name = "Podcasting",
                    Description = "Podcast hosts and audio creators.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000009"),
                    Name = "Education",
                    Description = "Educational creators and subject-matter experts.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000010"),
                    Name = "Comedy",
                    Description = "Comedians and entertainment creators.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000011"),
                    Name = "Art & Design",
                    Description = "Artists, illustrators and designers.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000012"),
                    Name = "Food",
                    Description = "Food, cooking and culinary creators.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000013"),
                    Name = "Travel",
                    Description = "Travel creators and explorers.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000014"),
                    Name = "Technology",
                    Description = "Technology creators and reviewers.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000015"),
                    Name = "Business",
                    Description = "Business, finance and entrepreneurship creators.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000016"),
                    Name = "Other",
                    Description = "Creators who do not fit another category.",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                });
        }
    }
}
