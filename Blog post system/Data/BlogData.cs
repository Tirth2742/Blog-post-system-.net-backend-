using Microsoft.EntityFrameworkCore;
using Blog_post_system.entities;
using System;

namespace Blog_post_system.Data
{
    public class BlogData : DbContext
    {
        public BlogData(DbContextOptions<BlogData> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Blogpost> BlogPosts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Define relationship: One User has many BlogPosts
            modelBuilder.Entity<Blogpost>()
                .HasOne(b => b.Author)
                .WithMany()
                .HasForeignKey(b => b.AuthorId)
                .OnDelete(DeleteBehavior.Cascade); // Delete posts when user is deleted

            base.OnModelCreating(modelBuilder);
        }
    }
}
