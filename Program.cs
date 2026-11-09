using RaceDay.Mvc.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o => { o.IdleTimeout = TimeSpan.FromMinutes(30); o.Cookie.Name = ".RaceDay.Mvc"; o.Cookie.HttpOnly = true; o.Cookie.IsEssential = true; });
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<RaceDayApiClient>((sp, client) => { var cfg = sp.GetRequiredService<IConfiguration>(); client.BaseAddress = new Uri(cfg["Api:BaseUrl"] ?? "https://localhost:53732/"); client.Timeout = TimeSpan.FromSeconds(30); });
builder.Services.AddScoped<ImageStorage>();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting(); app.UseSession();
app.MapControllerRoute(name: "default", pattern: "{controller=Events}/{action=Index}/{id?}");
app.Run();
