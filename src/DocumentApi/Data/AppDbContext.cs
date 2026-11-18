using DocumentApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentHistory> DocumentHistories => Set<DocumentHistory>();
}