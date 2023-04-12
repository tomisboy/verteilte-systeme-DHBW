using System.Text;

namespace Client.TrafficControl;

public class TrafficArea
{
    private const short NO_ID = -1;
    private short[][][] _area;
    
    public TrafficArea(short maxPerNode, short maxSizeX, short maxSizeY)
    {
        _area = new short[maxSizeX][][];
        for (var i = 0; i < maxSizeX; i++)
        {
            _area[i] = new short[maxSizeY][];
            
            for (var j = 0; j < maxSizeY; j++)
                _area[i][j] = new short[maxPerNode];
        }

        
        Clear();
    }
    
    public void Remove(short id, Coordinate from)
    {
        var idFoundAndRemoved = false;
        
        for (var i = 0; i < _area[from.X][from.Y].Length; i++)
        {
            if (_area[from.X][from.Y][i] != id) 
                continue;
            
            // id found start start
            _area[from.X][from.Y][i] = NO_ID;
            idFoundAndRemoved=true;
            break;
        }
        
        if (!idFoundAndRemoved) 
            throw new MovementNotPossible("id not found at start");
    }
    
    public void Place(short id, Coordinate to)
    {
        var freePos = -1;
        
        for (var i = 0; i < _area[to.X][to.Y].Length; i++) 
        {
            if (_area[to.X][to.Y][i] == -1)
                freePos = i;
            else if (_area[to.X][to.Y][i] == id)
                throw new MovementNotPossible("id already placed at target position");
        }
        
        if (freePos == -1)
            throw new MovementNotPossible("no empty space left");
        
        _area[to.X][to.Y][freePos] = id;
    }
    
    public Coordinate GetPosition(short id) 
    {
        for (short x = 0; x < _area.Length; x++) 
        {
            for (short y = 0; y < _area[x].Length; y++) 
            {
                for (var clientIdPos = 0; clientIdPos < _area[x][y].Length; clientIdPos++) 
                {
                    if (_area[x][y][clientIdPos] == id) 
                        return new Coordinate(x,y);
                }
            }
        }
        
        return null;
    }
    
    public bool IsFree(Coordinate position) 
    {
        var x = position.X;
        var y = position.Y;

        for (short clientIdPos = 0; clientIdPos < _area[x][y].Length; clientIdPos++)
        {
            if (_area[x][y][clientIdPos]==NO_ID) 
                return true;
        }
        
        return false;
    }
    
    public short[][][] GetArea() 
    {
        lock (this) 
        {
            return _area;
        }
    }

    public void SetArea(short[][][] area) 
    {
        lock (this)
        {
            _area = area;
        }
    }
    
    public void Print()
    {
        Console.WriteLine("############## AREA     ###############");

        for (var y = 0; y < _area[0].Length; y++)
        {
            foreach (var t in _area)
            {
                var toPrint = new StringBuilder("\t");
                
                for (var i = 0; i < t[y].Length; i++)
                    toPrint.Append(' ').Append(t[y][i]);

                Console.WriteLine("|" + toPrint + "|");
            }

            Console.WriteLine("");
        }
        
        Console.WriteLine("############## AREA END ###############");
    }
    
    public void Clear()
    {
        foreach (var x in _area)
        {
            foreach (var y in x)
            {
                for (var clientIdPos = 0; clientIdPos < y.Length; clientIdPos++)
                    y[clientIdPos] = NO_ID;
            }
        }
    }
}