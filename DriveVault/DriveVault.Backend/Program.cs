using DriveVault.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddControllers();
var app = builder.Build();
app.UseCors();
app.MapGet("/api/drives", () => Results.Ok(DatabaseHelper.GetAllDrives()));
app.MapGet("/api/folders/{driveId}", (string driveId) => Results.Ok(DatabaseHelper.GetFoldersByDrive(driveId)));
app.MapGet("/api/clients", () => Results.Ok(DatabaseHelper.GetAllClients()));
app.MapGet("/api/activity", (int days) => Results.Ok(DatabaseHelper.GetRecentActivity(days)));
app.MapPost("/api/log", (ActivityLog log) => { DatabaseHelper.LogActivity(log.EventType, log.DriveId, log.FolderId, log.FolderName, log.DriveName); return Results.Ok(); });

app.Run();