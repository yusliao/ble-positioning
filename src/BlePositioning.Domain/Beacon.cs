namespace BlePositioning.Domain;

public class Beacon
{
    public Guid Id { get; private set; }
    public Guid FloorId { get; private set; }
    public string Uuid { get; private set; } = null!;
    public int Major { get; private set; }
    public int Minor { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public int TxPower { get; private set; }
    public BeaconStatus Status { get; private set; }
    public bool IsDeleted { get; private set; }

    private Beacon()
    {
    }

    public static Beacon Create(Guid floorId, string uuid, int major, int minor, double x, double y, int txPower)
    {
        return new Beacon
        {
            Id = Guid.NewGuid(),
            FloorId = floorId,
            Uuid = uuid,
            Major = major,
            Minor = minor,
            X = x,
            Y = y,
            TxPower = txPower,
            Status = BeaconStatus.Active,
            IsDeleted = false,
        };
    }

    public void Move(double x, double y, int txPower)
    {
        X = x;
        Y = y;
        TxPower = txPower;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
    }
}
