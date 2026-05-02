using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpawnPositionsManager : NetworkBehaviour
{
    [SerializeField] int totalPlayers = 6;
    public NetworkList<int> spawnPositionsIndices = new NetworkList<int>();


    private void Awake()
    {
        
        SetupRandomSpawnOrder();
    }
    void Start()
    {
        
    }

    
    void Update()
    {
        
    }

    void SetupRandomSpawnOrder()
    {
        if (!IsServer) return; 

        List<int> tempList = new List<int>();

        for (int i = 1; i <= totalPlayers; i++)
        {
            tempList.Add(i);
        }
        for (int i = 0; i < tempList.Count; i++)
        {
            int randomIndex = Random.Range(i, tempList.Count);
            int tmp = tempList[i];
            tempList[i] = tempList[randomIndex];
            tempList[randomIndex] = tmp;
        }

        spawnPositionsIndices.Clear(); 
        foreach (int n in tempList)
        {
            spawnPositionsIndices.Add(n);
        }
    }
}
