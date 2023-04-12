namespace ServerNode.TrafficControl;

public class TrafficControlLogic
{
    public readonly TrafficArea TrafficArea;

    public TrafficControlLogic(TrafficArea trafficArea)
    {
        TrafficArea = trafficArea;
    }

    public Coordinate Start(short id)
    {
        var currentPosition = TrafficArea.GetPosition(id);

        if (currentPosition != null)
            throw new MovementNotPossible("client already available");

        // client not found, this is fine
        for (short y = 0; y < TrafficArea.GetArea()[0].Length; y++)
        {
            var pos = new Coordinate(0, y);

            if (!TrafficArea.IsFree(pos))
                continue;

            TrafficArea.Place(id, pos);
            return pos;
        }

        throw new MovementNotPossible("no free position found");
    }

    public Coordinate Move(short id, Coordinate targetToReach)
    {
        // get the current position of the client
        var currentPosition = TrafficArea.GetPosition(id)!;
        // calculate the next step around the current position
        var bestCoordinate = currentPosition;
        var distance = GetDistance(bestCoordinate, targetToReach);

        for (var xOffset = -1; xOffset <= 1; xOffset++)
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        {
            var x = (short) (currentPosition.X + xOffset);
            var y = (short) (currentPosition.Y + yOffset);

            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x > TrafficArea.GetArea().Length - 1) x = (short) (TrafficArea.GetArea().Length - 1);
            if (y > TrafficArea.GetArea()[0].Length - 1) y = (short) (TrafficArea.GetArea()[0].Length - 1);

            var coordinateToCheck = new Coordinate(x, y);
            
            if (!TrafficArea.IsFree(coordinateToCheck)) 
                continue;
            
            var newDistance = GetDistance(coordinateToCheck, targetToReach);
            
            if (!(newDistance < distance)) 
                continue;
            
            distance = newDistance;
            bestCoordinate = coordinateToCheck;
        }

        // new / old coordinate determined
        // update the area
        TrafficArea.Remove(id, currentPosition);
        TrafficArea.Place(id, bestCoordinate);
        return bestCoordinate;
    }

    private double GetDistance(Coordinate firstCoordinate, Coordinate secondCoordinate)
    {
        double x1 = firstCoordinate.X;
        double x2 = secondCoordinate.X;
        double y1 = firstCoordinate.Y;
        double y2 = secondCoordinate.Y;

        if (x1 == x2 && y1 == y2)
            return 0.0;
        
        // calculate the distance
        return Math.Sqrt(
            Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2)
        );
    }
}