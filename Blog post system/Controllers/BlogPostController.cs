﻿using Blog_post_system.Data;
using Blog_post_system.entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Blog_post_system.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogPostController : ControllerBase
    {
        private readonly BlogData data;
        private readonly IConfiguration _configuration;
        public BlogPostController(BlogData context  , IConfiguration configuration)
        {
            _configuration = configuration;
            data = context;
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllBlog()
        {
            var blogs = await data.BlogPosts
                .Include(a => a.Author)
                .Select(a=> new
                {
                    a.Id,
                    a.Title,
                    a.Context,
                    a.Category,
                    AuthorName = a.Author.Username,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .ToListAsync();
            return Ok(blogs);
        }
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBlogById(Guid id)
        {
            var blogs = await data.BlogPosts
                .Include(a => a.Author)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Context,
                    a.Category,
                    AuthorName = a.Author.Username,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .FirstOrDefaultAsync(a=>a.Id == id);
            return Ok(blogs);
        }
        [Authorize]
        [HttpGet("mypost/{authorId}")]
        public async Task<IActionResult> GetAllMyBlog(Guid authorId)
        {
            var blogs = await data.BlogPosts
                .Include(a => a.Author)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Context,
                    a.Category,
                    AuthorName = a.Author.Username,
                    aid = a.Author.Id,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .Where(a => a.aid == authorId)
                .ToListAsync();
            return Ok(blogs);
        }
        [HttpPost("adduser")]
        public async Task<IActionResult> AddUser(User user)
        {
            data.Users.Add(user);
            await data.SaveChangesAsync();
            return Ok(user);
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] JsonElement login)
        {
            string email = login.GetProperty("email").GetString();
            string password = login.GetProperty("password").GetString();

            var user = await data.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || password != user.Password)
            {
                return Unauthorized("Invalid email or password.");
            }

            // Generate JWT Token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        }),
                Expires = DateTime.UtcNow.AddMinutes(60),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new { Token = tokenString, UserId = user.Id, Username = user.Username });
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PublishBlog( Blogpost blog)
        {
            if (blog == null)
            {
                return BadRequest("Blog data is required.");
            }

            // Ensure the author exists
            var author = await data.Users.FirstOrDefaultAsync(a=>a.Id == blog.AuthorId);
            Console.WriteLine(author.ToString);
            if (author == null)
            {
                return NotFound("Author not found.");
            }

            // Prevent EF from expecting 'Author' in JSON input
            blog.Author = author;
            //blog.Id = Guid.NewGuid();
            blog.CreatedAt = DateTime.UtcNow;

            data.BlogPosts.Add(blog);
            await data.SaveChangesAsync();

            return Ok("successfully created");
        }
        [Authorize]
        [HttpGet("filter")]
        public async Task<IActionResult> BlogFilter(string? category , string ?author , string? daterange , DateTime?fromdate , DateTime? todate)
        {
            var blogs = data.BlogPosts
                        .Include(a => a.Author)
                        .Select(a => new
                        {
                            a.Id,
                            a.Title,
                            a.Context,
                            a.Category,
                            AuthorName = a.Author.Username,
                            a.CreatedAt,
                            a.UpdatedAt
                        })
                        .AsQueryable();
            if (!string.IsNullOrEmpty(category))
            {
                blogs = blogs.Where(b => b.Category.Contains(category));
            }
            
            if (!string.IsNullOrEmpty(daterange))
            {
                DateTime now = DateTime.UtcNow;
                if (daterange.Equals("lastmonth", StringComparison.OrdinalIgnoreCase))
                {
                    fromdate = now.AddMonths(-1);
                }
                else if (daterange.Equals("lastyear", StringComparison.OrdinalIgnoreCase))
                {
                    fromdate = now.AddYears(-1);
                }
            }
            if (fromdate.HasValue)
            {
                blogs = blogs.Where(b => b.CreatedAt >= fromdate.Value);
            }
            if (todate.HasValue)
            {
                blogs = blogs.Where(b => b.CreatedAt <= todate.Value);
            }
            return Ok(blogs.ToList());

        }
        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> BlogSearch(string? st)
        {
            var blogs =  data.BlogPosts
                        .Include(a => a.Author)
                        .Select(a => new
                        {
                            a.Id,
                            a.Title,
                            a.Context,
                            a.Category,
                            AuthorName = a.Author.Username,
                            a.CreatedAt,
                            a.UpdatedAt
                        })
                        .AsQueryable();
            if (!string.IsNullOrEmpty(st))
            {
                blogs = blogs.Where(b => b.Context.Contains(st) || b.Title.Contains(st));
            }
         
            return Ok(blogs.ToList());
        }
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> BlogEdit([FromBody] JsonElement updatedBlog , Guid id)
        {
            var existingBlog = await data.BlogPosts.FindAsync(id);
            string title = updatedBlog.GetProperty("title").GetString();
            string context = updatedBlog.GetProperty("context").GetString();
            string category = updatedBlog.GetProperty("category").GetString();
            if (!string.IsNullOrEmpty(title))
                existingBlog.Title = title;
            if (!string.IsNullOrEmpty(category))
                existingBlog.Category = category;
            if (!string.IsNullOrEmpty(context))
                existingBlog.Context = context;
            existingBlog.UpdatedAt = DateTime.UtcNow;

            await data.SaveChangesAsync();
            return Ok("updated successfully");

        }
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlogPost(Guid id)
        {
            var blog = await data.BlogPosts.FindAsync(id);
            if (blog == null) return NotFound();
            data.BlogPosts.Remove(blog);
            await data.SaveChangesAsync();
            return Ok("successfully deleted");
        }


    }
}
