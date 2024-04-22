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
    [SerializeField, Range(0, 1)]
    private float[] _intrabiomThresholds;
    [SerializeField]
    private GameObject[] _forestTiles, _swampTiles, _seaTiles;

    private Dictionary<float, GameObject[]> _biomesByHeightThreshold = new Dictionary<float, GameObject[]>();
    private List<List<GameObject>> _tileGrid = new List<List<GameObject>>();

    void Start()
    {
        SetupAndCheckThresholds();
        GenerateBiomes();
        Camera.main.transform.position = new Vector3((_mapWidth - 1)/2f, (_mapHeight - 1)/2f, Camera.main.transform.position.z);
    }

    private void GenerateBiomes()
    {
        for (int x = 0; x < _mapWidth; x++)
        {
            _tileGrid.Add(new List<GameObject>());
            for (int y = 0; y < _mapHeight; y++)
                _tileGrid[x].Add(Instantiate(GetTileUsingPerlin(x, y), new Vector2(x,y), Quaternion.identity, transform));
        }
    }

    private GameObject GetTileUsingPerlin(int x, int y)
    {
        var perlinValue = Mathf.Clamp01(Mathf.PerlinNoise((x + _mapOffest.x) / _biomeSizes, (y + _mapOffest.y) / _biomeSizes));
        foreach (var biomeByThreshold in _biomesByHeightThreshold)
        {
            if (biomeByThreshold.Key <= perlinValue)
            {
                perlinValue = Mathf.Clamp01(Mathf.PerlinNoise((x - _mapOffest.x) / _biomeSizes, (y - _mapOffest.y) / _biomeSizes));
                for (int i = 0; i < _intrabiomThresholds.Length; i++)
                    if (_intrabiomThresholds[i] <= perlinValue)
                        return biomeByThreshold.Value[i];
            }
        }
        Debug.LogError(nameof(GetTileUsingPerlin) + " didn't find proper tile!");
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
    }
}
