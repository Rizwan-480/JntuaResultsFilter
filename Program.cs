var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<JntuaResultsFilter.Database.DatabaseHelper>();
builder.Services.AddHttpClient<JntuaResultsFilter.Services.JntuaApiService>()
    .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
    {
        ConnectCallback = async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host;
            var port = context.DnsEndPoint.Port;
            if (host.Equals("jntuaresults.ac.in", StringComparison.OrdinalIgnoreCase))
            {
                var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                await socket.ConnectAsync("143.244.131.73", port, cancellationToken);
                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            }
            else
            {
                var addresses = await System.Net.Dns.GetHostAddressesAsync(host, cancellationToken);
                var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                await socket.ConnectAsync(addresses, port, cancellationToken);
                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            }
        }
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Results}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
