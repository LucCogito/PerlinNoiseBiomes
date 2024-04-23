using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [SerializeField, Range(0, 100)]
    private int _mapWidth, _mapHeight;
    [SerializeField, Range(5, 20)]
    private float _biomeSizes;
    [SerializeField]
    private Vector2 _mapOffest;
    [SerializeField, Range(0, 1)]
    private float _forestHeightThreshold, _swampHeightThreshold;
    [SerializeField, Range(.8f, .5f)]
    private float _riverDirectionCoefficient;
    [SerializeField, Range(0, 1)]
    private float[] _intrabiomThresholds;
    [SerializeField]
    private GameObject[] _forestTiles, _swampTiles, _seaTiles;
    [SerializeField]
    private GameObject _riverTile;


    private Dictionary<float, GameObject[]> _biomesByHeightThreshold = new Dictionary<float, GameObject[]>();
    private bool[,] _occupiedMapPositions;
    private float[,] _perlinMap;
    private List<Vector2Int> _mountainPeaks = new List<Vector2Int>();
    private List<Vector2Int> _lakeBottoms = new List<Vector2Int>();
    private Vector2Int[] _neighbourTileDirections;
    private Vector2Int[][] _riverDirections;

    void Start()
    {
        SetupAndCheckThresholds();
        GenerateRivers();
        GenerateBiomes();
        Camera.main.transform.position = new Vector3((_mapWidth - 1)/2f, (_mapHeight - 1)/2f, Camera.main.transform.position.z);
    }

    private void GenerateRivers()
    {
        FindMountainsAndLakes();
        FlowRiversDownTheMountains();
    }

    private void FindMountainsAndLakes()
    {
        float perlinValue;
        for (int x = 0; x < _mapWidth; x++)
        {
            for (int y = 0; y < _mapHeight; y++)
            {
                perlinValue = _perlinMap[x, y];
                if (perlinValue >= _intrabiomThresholds[0] * (1 - _forestHeightThreshold) + _forestHeightThreshold)
                {
                    if (CompareToNeighbours(x, y, perlinValue, true))
                        _mountainPeaks.Add(new Vector2Int(x,y));
                    continue;
                }
                if (perlinValue < _swampHeightThreshold)
                {
                    if (CompareToNeighbours(x, y, perlinValue, false))
                        _lakeBottoms.Add(new Vector2Int(x, y));
                    continue;
                }
            }
        }
    }

    private bool CompareToNeighbours(int x, int y, float height, bool findHigher)
    {
        Vector2Int neighbourPosition;
        foreach (var direction in _neighbourTileDirections)
        {
            neighbourPosition = new Vector2Int(x + direction.x, y + direction.y);
            if (!WithinBounds(neighbourPosition))
                continue;
            if (findHigher && (_perlinMap[neighbourPosition.x, neighbourPosition.y] > height))
                return false;
            else if (!findHigher && (_perlinMap[neighbourPosition.x, neighbourPosition.y] < height))
                return false;
        }
        return true;
    }

    private void FlowRiversDownTheMountains()
    {
        float currentDistance, closestLakeDistance;
        Vector2Int closestLake = Vector2Int.zero;
        foreach (var peak in _mountainPeaks)
        {
            closestLakeDistance = float.MaxValue;
            foreach (var lake in _lakeBottoms)
            {
                currentDistance = Vector2Int.Distance(peak, lake);
                if (currentDistance < closestLakeDistance)
                {
                    closestLakeDistance = currentDistance;
                    closestLake = lake;
                }
            }
            CreateRiver(peak, closestLake);
        }
    }

    private void CreateRiver(Vector2Int start, Vector2Int finish)
    {
        float currentTileHeight;
        Vector2 globalDirection;
        Vector2Int[] directionCandidates;
        Vector2Int currentTile, lowestTile;
        while (true)
        {
            globalDirection.x = finish.x - start.x;
            globalDirection.y = finish.y - start.y;
            if (Mathf.Abs(globalDirection.x * _riverDirectionCoefficient) > Mathf.Abs(globalDirection.y))
                directionCandidates = (globalDirection.x > 0) ? _riverDirections[2] : _riverDirections[6];
            else if (Mathf.Abs(globalDirection.y * _riverDirectionCoefficient) > Mathf.Abs(globalDirection.x))
                directionCandidates = (globalDirection.y > 0) ? _riverDirections[0] : _riverDirections[4];
            else
                directionCandidates = (globalDirection.y > 0) ? 
                    (globalDirection.x > 0 ? _riverDirections[1] : _riverDirections[7]) : 
                    (globalDirection.x > 0 ? _riverDirections[3] : _riverDirections[5]);

            lowestTile = start + directionCandidates[1];
            for (int i = 2; i != 1; i = (i+1) % 2)
            {
                currentTile = start + directionCandidates[i];
                if (WithinBounds(currentTile) && !_occupiedMapPositions[currentTile.x, currentTile.y] &&
                    _perlinMap[currentTile.x, currentTile.y] < _perlinMap[lowestTile.x, lowestTile.y])
                {
                    lowestTile = currentTile;
                }
            }
            currentTileHeight = _perlinMap[lowestTile.x, lowestTile.y];
            start.x = lowestTile.x; start.y = lowestTile.y;

            if (currentTileHeight >= _swampHeightThreshold)
            {
                Instantiate(_riverTile, new Vector2(start.x, start.y), Quaternion.identity, transform);
                _occupiedMapPositions[start.x, start.y] = true;
            }
            else
                return;
        }
    }

    private bool WithinBounds(Vector2Int position) => position.x >= 0 && position.x < _mapWidth && position.y >= 0 && position.y < _mapHeight;

    private void GenerateBiomes()
    {
        for (int x = 0; x < _mapWidth; x++)
        {
            for (int y = 0; y < _mapHeight; y++)
            {
                if (!_occupiedMapPositions[x,y])
                {
                    Instantiate(GetTileUsingPerlinMap(x, y), new Vector2(x,y), Quaternion.identity, transform);
                    _occupiedMapPositions[x,y] = true;
                }
            }
        }
    }

    private GameObject GetTileUsingPerlinMap(int x, int y)
    {
        var perlinValue = _perlinMap[x,y];
        float previousThreshold = 1f;
        foreach (var biomeByThreshold in _biomesByHeightThreshold)
        {
            if (biomeByThreshold.Key <= perlinValue)
            {
                perlinValue = (perlinValue - biomeByThreshold.Key) / (previousThreshold - biomeByThreshold.Key);
                for (int i = 0; i < _intrabiomThresholds.Length; i++)
                    if (_intrabiomThresholds[i] <= perlinValue)
                        return biomeByThreshold.Value[i];
            }
            previousThreshold = biomeByThreshold.Key;
        }
        Debug.LogError(nameof(GetTileUsingPerlinMap) + " didn't find proper tile!");
        return null;
    }

    private void SetupAndCheckThresholds()
    {
        if (_swampHeightThreshold > _forestHeightThreshold)
        {
            Debug.LogError("Swamp must be lower than forest!");
            return;
        }
        for (int i = 1; i < _intrabiomThresholds.Length; i++)
        {
            if (_intrabiomThresholds[i] > _intrabiomThresholds[i - 1])
            {
                Debug.LogError($"Tiles thresholds must be in descending order!");
                return;
            }
        }
        _biomesByHeightThreshold.Add(_forestHeightThreshold, _forestTiles);
        _biomesByHeightThreshold.Add(_swampHeightThreshold, _swampTiles);
        _biomesByHeightThreshold.Add(0f, _seaTiles);
        _occupiedMapPositions = new bool[_mapWidth, _mapHeight];
        _perlinMap = new float[_mapWidth, _mapHeight];
        for (int x = 0; x < _mapWidth; x++)
            for (int y = 0; y < _mapHeight; y++)
                _perlinMap[x,y] = Mathf.Clamp01(Mathf.PerlinNoise((x + _mapOffest.x) / _biomeSizes, (y + _mapOffest.y) / _biomeSizes));
        _neighbourTileDirections = new Vector2Int[]
        {
            new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(1,0), new Vector2Int(1,-1),
            new Vector2Int(0,-1), new Vector2Int(-1,-1), new Vector2Int(-1,0), new Vector2Int(-1,1)
        };
        _riverDirections = new Vector2Int[][]
        {
            new Vector2Int[]{_neighbourTileDirections[7], _neighbourTileDirections[0], _neighbourTileDirections[1]},
            new Vector2Int[]{_neighbourTileDirections[0], _neighbourTileDirections[1], _neighbourTileDirections[2]},
            new Vector2Int[]{_neighbourTileDirections[1], _neighbourTileDirections[2], _neighbourTileDirections[3]},
            new Vector2Int[]{_neighbourTileDirections[2], _neighbourTileDirections[3], _neighbourTileDirections[4]},
            new Vector2Int[]{_neighbourTileDirections[3], _neighbourTileDirections[4], _neighbourTileDirections[5]},
            new Vector2Int[]{_neighbourTileDirections[4], _neighbourTileDirections[5], _neighbourTileDirections[6]},
            new Vector2Int[]{_neighbourTileDirections[5], _neighbourTileDirections[6], _neighbourTileDirections[7]},
            new Vector2Int[]{_neighbourTileDirections[6], _neighbourTileDirections[7], _neighbourTileDirections[0]}
        };
    }
}
