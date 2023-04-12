namespace Client.TrafficControl;

public class MovementNotPossible : Exception
{
    public MovementNotPossible(string message) : base(message) { }
}