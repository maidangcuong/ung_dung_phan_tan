using DocumentApi.Data;
using DocumentApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình kết nối PostgreSQL
var connStr = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5433;Database=doc_approval_db;Username=admin;Password=password123";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));

var app = builder.Build();

// Tự động tạo bảng Database khi khởi chạy ứng dụng
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// --- 6 ENDPOINTS TỐI THIỂU CHO GIAI ĐOẠN 2 ---

// 1. POST /documents: Tạo tài liệu mới (Trạng thái mặc định: Draft)
app.MapPost("/documents", async (CreateDocDto dto, AppDbContext db) =>
{
    var doc = new Document 
    { 
        Title = dto.Title, 
        Content = dto.Content, 
        CreatedBy = dto.CreatedBy,
        Status = DocumentStatus.Draft 
    };
    
    doc.Histories.Add(new DocumentHistory 
    { 
        Action = "Created", 
        PerformedBy = dto.CreatedBy, 
        Note = "Tạo bản nháp tài liệu" 
    });
    
    db.Documents.Add(doc);
    await db.SaveChangesAsync();
    return Results.Created($"/documents/{doc.Id}", doc);
});

// 2. GET /documents: Lấy danh sách tài liệu (hỗ trợ lọc theo status hoặc actor)
app.MapGet("/documents", async (string? status, string? actor, AppDbContext db) =>
{
    var query = db.Documents.AsQueryable();
    if (!string.IsNullOrEmpty(status) && Enum.TryParse<DocumentStatus>(status, true, out var st))
        query = query.Where(d => d.Status == st);
    if (!string.IsNullOrEmpty(actor))
        query = query.Where(d => d.CreatedBy == actor || d.Approver == actor);

    return Results.Ok(await query.ToListAsync());
});

// 3. GET /documents/{id}: Xem chi tiết tài liệu kèm lịch sử xử lý
app.MapGet("/documents/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var doc = await db.Documents.Include(d => d.Histories).FirstOrDefaultAsync(d => d.Id == id);
    return doc is null ? Results.NotFound(new { message = "Không tìm thấy tài liệu" }) : Results.Ok(doc);
});

// 4. POST /documents/{id}/submit: Gửi tài liệu vào quy trình (Draft -> Submitted) + State Validation
app.MapPost("/documents/{id:guid}/submit", async (Guid id, ActionDto dto, AppDbContext db) =>
{
    var doc = await db.Documents.FindAsync(id);
    if (doc is null) return Results.NotFound(new { message = "Không tìm thấy tài liệu" });
    
    // State Validation: Chỉ cho phép gửi khi đang ở trạng thái Draft
    if (doc.Status != DocumentStatus.Draft) 
        return Results.BadRequest(new { message = "Chỉ tài liệu ở trạng thái Draft mới được phép gửi duyệt." });

    doc.Status = DocumentStatus.Submitted;
    db.DocumentHistories.Add(new DocumentHistory 
    { 
        DocumentId = doc.Id, 
        Action = "Submitted", 
        PerformedBy = dto.Actor, 
        Note = dto.Note ?? "Gửi tài liệu chờ duyệt" 
    });
    
    await db.SaveChangesAsync();
    return Results.Ok(doc);
});

// 5. POST /documents/{id}/approve: Phê duyệt tài liệu (Submitted -> Approved) + State Validation
app.MapPost("/documents/{id:guid}/approve", async (Guid id, ActionDto dto, AppDbContext db) =>
{
    var doc = await db.Documents.FindAsync(id);
    if (doc is null) return Results.NotFound(new { message = "Không tìm thấy tài liệu" });

    // State Validation: Chỉ cho phép duyệt khi đang ở trạng thái Submitted
    if (doc.Status != DocumentStatus.Submitted) 
        return Results.BadRequest(new { message = "Tài liệu không ở trạng thái chờ duyệt (Submitted) nên không thể phê duyệt." });

    doc.Status = DocumentStatus.Approved;
    doc.Approver = dto.Actor;
    db.DocumentHistories.Add(new DocumentHistory 
    { 
        DocumentId = doc.Id, 
        Action = "Approved", 
        PerformedBy = dto.Actor, 
        Note = dto.Note ?? "Phê duyệt tài liệu" 
    });
    
    await db.SaveChangesAsync();
    return Results.Ok(doc);
});

// 6. POST /documents/{id}/reject: Từ chối tài liệu (Submitted -> Rejected) + State Validation
app.MapPost("/documents/{id:guid}/reject", async (Guid id, ActionDto dto, AppDbContext db) =>
{
    var doc = await db.Documents.FindAsync(id);
    if (doc is null) return Results.NotFound(new { message = "Không tìm thấy tài liệu" });

    // State Validation: Chỉ cho phép từ chối khi đang ở trạng thái Submitted
    if (doc.Status != DocumentStatus.Submitted) 
        return Results.BadRequest(new { message = "Tài liệu không ở trạng thái chờ duyệt (Submitted) nên không thể từ chối." });

    doc.Status = DocumentStatus.Rejected;
    doc.Approver = dto.Actor;
    db.DocumentHistories.Add(new DocumentHistory 
    { 
        DocumentId = doc.Id, 
        Action = "Rejected", 
        PerformedBy = dto.Actor, 
        Note = dto.Note ?? "Từ chối tài liệu" 
    });
    
    await db.SaveChangesAsync();
    return Results.Ok(doc);
});

app.Run();

// DTO records hỗ trợ nhận dữ liệu từ Client gửi lên
record CreateDocDto(string Title, string Content, string CreatedBy);
record ActionDto(string Actor, string? Note);