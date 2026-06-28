namespace MangaERP.Api.Models.Sam;

/// <summary>
/// Response returned by the SAM Python service after computing an image embedding.
/// </summary>
public class EmbeddingResponse
{
    /// <summary>Base-64 encoded embedding tensor.</summary>
    public string Embedding { get; set; } = string.Empty;

    /// <summary>Shape of the embedding tensor, e.g. [1, 256, 64, 64].</summary>
    public int[] Shape { get; set; } = [];

    /// <summary>Numpy dtype string, e.g. "float32".</summary>
    public string Dtype { get; set; } = string.Empty;

    /// <summary>Original image dimensions [height, width].</summary>
    public int[] ImageSize { get; set; } = [];
}

/// <summary>
/// Request payload sent to the SAM Python service for mask prediction.
/// Contains the pre-computed embedding plus a click point.
/// </summary>
public class PredictRequest
{
    /// <summary>Base-64 encoded embedding tensor (from EmbeddingResponse).</summary>
    public string Embedding { get; set; } = string.Empty;

    /// <summary>Shape of the embedding tensor.</summary>
    public int[] Shape { get; set; } = [];

    /// <summary>Numpy dtype string.</summary>
    public string Dtype { get; set; } = string.Empty;

    /// <summary>Original image dimensions [height, width].</summary>
    public int[] ImageSize { get; set; } = [];

    /// <summary>X-coordinate of the click point (in image pixel space).</summary>
    public float X { get; set; }

    /// <summary>Y-coordinate of the click point (in image pixel space).</summary>
    public float Y { get; set; }
}

/// <summary>
/// Response returned by the SAM Python service after predicting a mask.
/// </summary>
public class MaskResponse
{
    /// <summary>
    /// Run-Length Encoding (RLE) of the predicted mask.
    /// Can be a plain RLE dict or a COCO-format RLE — kept as <see cref="object"/>
    /// so the raw JSON structure is preserved for the frontend.
    /// </summary>
    public object? MaskRle { get; set; }

    /// <summary>Confidence score of the predicted mask (0–1).</summary>
    public float Score { get; set; }

    /// <summary>Bounding box of the mask [x, y, width, height].</summary>
    public int[] Bbox { get; set; } = [];
}
