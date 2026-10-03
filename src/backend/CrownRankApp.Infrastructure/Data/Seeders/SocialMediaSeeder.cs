using CrownRankApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Infrastructure.Data.Seeders
{
    public static class SocialMediaSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SocialMediaDefault>().HasData(
                    new
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                    Name = "Instagram",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                    Name = "TikTok",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000003"),
                    Name = "YouTube",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000004"),
                    Name = "OnlyFans",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000005"),
                    Name = "Facebook",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000006"),
                    Name = "X",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000007"),
                    Name = "Twitch",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                },
                    new
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000008"),
                    Name = "Website",
                    CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedDate = (DateTime?)null
                });
        }
    }
}
