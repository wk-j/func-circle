module FuncCircle.Library

open System
open SixLabors.ImageSharp
open SixLabors.Shapes
open SixLabors.ImageSharp.PixelFormats
open SixLabors.Primitives
open SixLabors.ImageSharp.Processing
open System.IO
open System.Net.Http
open Microsoft.Azure.WebJobs.Host
open Microsoft.AspNetCore.Http
open System.Net.Http
open SixLabors.ImageSharp.Formats
open SixLabors.ImageSharp.Formats.Png
open System.Net
open Microsoft.Net.Http.Headers
open System.Text

let buildCorner width height radius = 
    let rect = RectangularePolygon(-0.5f, -0.5f, radius, radius)
    let cornerToptLeft = rect.Clip(EllipsePolygon(radius - 0.5f, radius - 0.5f, radius))
    let rightPos = float32 width - cornerToptLeft.Bounds.Width + 1.0f
    let bottomPos = float32 height - cornerToptLeft.Bounds.Height + 1.0f
    let cornerTopRight = cornerToptLeft.RotateDegree(90.0f).Translate(rightPos, 0.0f);
    let cornerBottomLeft = cornerToptLeft.RotateDegree(-90.0f).Translate(0.0f, bottomPos);
    let cornerBottomRight = cornerToptLeft.RotateDegree(-180.0f).Translate(rightPos, bottomPos);

    PathCollection(cornerToptLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight)

let applyRoundedCorners (img: Image<Rgba32>) radius = 
    let corners = buildCorner img.Width img.Height radius
    let mutate (img: IImageProcessingContext<Rgba32>) = 
        let opt = GraphicsOptions(true, BlenderMode = PixelBlenderMode.Src)
        img.Fill(Rgba32.Transparent, corners, opt) |> ignore
    img.Mutate(fun x -> mutate(x))

let convertToAvatar(img: IImageProcessingContext<Rgba32>) (size: Size) radius = 
    let rs = img.Resize(ResizeOptions(Size = size, Mode = ResizeMode.Crop))
    rs.Apply(fun x -> applyRoundedCorners x radius)

let cloneAndConvertToAvatarWithoutApply(img: Image<Rgba32>) (size: Size) radius = 
    let result = img.Clone(fun x -> x.Resize(ResizeOptions(Size = size, Mode = ResizeMode.Crop)) |> ignore)
    applyRoundedCorners result radius

let isUrl path = (path: string).StartsWith("http")

let downloadStream httpPath = 
    use client = new HttpClient()
    let rs = client.GetAsync(httpPath: string) |> Async.AwaitTask |> Async.RunSynchronously
    rs.Content.ReadAsByteArrayAsync() |> Async.AwaitTask |> Async.RunSynchronously

let processImage (log: TraceWriter) path = 
    log.Info <| sprintf "process file - %s" path

    let bytes = downloadStream path

    use img = Image.Load(bytes)
    use round = img.Clone(fun x -> convertToAvatar x (Size(300, 300)) 150.0f |> ignore) 
    use memory = new MemoryStream() 
    round.Save(memory, PngEncoder())
    memory.ToArray()

let run (req: HttpRequest, log: TraceWriter) = 
    let ok, imageUrl = req.Query.TryGetValue("imageUrl")

    if ok then
        let url = imageUrl.ToString();
        log.Info <| sprintf "url - %s" url

        let images = processImage log url
        let message = 
            use result = new HttpResponseMessage(HttpStatusCode.OK)
            result.Content <- new ByteArrayContent(images)
            result.Content.Headers.ContentDisposition <-
                let content = Headers.ContentDispositionHeaderValue("attachment")
                content.FileName <- "result.png"
                content
            result.Content.Headers.ContentType <-
                Headers.MediaTypeHeaderValue("image/png")
            result
        message
    else
        new HttpResponseMessage(HttpStatusCode.OK)
