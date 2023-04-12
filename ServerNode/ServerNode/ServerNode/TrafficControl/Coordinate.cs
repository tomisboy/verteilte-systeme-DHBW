namespace ServerNode.TrafficControl;

public class Coordinate
{
    public short X { get; set; }= -1;
    public short Y { get; set; } = -1;
    
    public Coordinate(short x, short y) 
    {
        X = x;
        Y = y;
    }
}