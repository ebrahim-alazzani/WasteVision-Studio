namespace WasteVision.Desktop.Models;

public sealed class Hyperparameters
{
    public int Epochs { get; set; } = 12;
    public int ImageSize { get; set; } = 160;
    public int BatchSize { get; set; } = 16;
    public int Filters { get; set; } = 32;
    public int TestPercent { get; set; } = 15;
    public int ValidationPercent { get; set; } = 15;
    public double Dropout { get; set; } = 0.35;
    public double LearningRate { get; set; } = 0.001;
    public bool Augmentation { get; set; } = true;

    public Dictionary<string, object> ToPayload() => new()
    {
        ["epochs"] = Epochs,
        ["image_size"] = ImageSize,
        ["batch_size"] = BatchSize,
        ["filters"] = Filters,
        ["test_percent"] = TestPercent,
        ["validation_percent"] = ValidationPercent,
        ["dropout"] = Dropout,
        ["learning_rate"] = LearningRate,
        ["augmentation"] = Augmentation
    };
}
