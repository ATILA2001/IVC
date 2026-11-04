using IVC;

// El detalle de cada bloque vive en Extensions/ (IvcBuilderExtensions y
// IvcPipelineExtensions), un método por concern y en este mismo orden.
var builder = WebApplication.CreateBuilder(args);

builder.AddPresentation()
       .AddDatabases()
       .AddSharedCookieAuth()
       .AddApplicationServices()
       .AddHttpsHardening();

var app = builder.Build();

app.UseIvcPipeline()
   .MapIvcEndpoints()
   .StartDatabaseWarmup();

await app.RunAsync();
