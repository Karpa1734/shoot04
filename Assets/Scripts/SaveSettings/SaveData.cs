using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveData : ScriptableObject
{
    [SerializeField]
    private string FilePath = @".\save.dat";
    [SerializeField]
    private List<BitLayout> Save;
    private static SaveData _instance;
    public static SaveData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<SaveData>(nameof(SaveData));
            }

            return _instance;
        }
    }
    public static string Path
    {
        get { return Instance.FilePath; }
    }
    public static List<BitLayout> Saves
    {
        get { return Instance.Save; }
    }
}
