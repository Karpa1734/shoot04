/*
 * 使用方法 :
 * SaveManager.cs, BitContainer.cs, SaveData.cs, SaveSettings.csをUnityにインポートしてください。
 * 正常にインポート出来ていればProject Settingsに "Save Data Settings" が追加されます。
 * Save Data Settingsの設定内容は以下の通りです。
 * 
 * FilePath : 生成されるセーブデータの位置と名前
 * Save :
 *      Name   : 書き込み・読み込みに使用する名前
 *      Length : 保存するデータ領域の大きさ
 *      Unit   : Lengthに対応する単位
 *      
 * また、セーブデータの設定はSaveManager.Add(delegate)に以下のようなデリゲートを渡すことでも追加できます。
 * ただし、必ずセーブデータの読み書きを行う前に使用してください。
 * 　デリゲートの形式 : List<BitLayout> (void);
 * 　
 * 　読み書きは SaveManager.Save 及び SaveManager.Load で行います。
 * 　データの保存は下位ビット優先で行われ、保存先の大きさが不足している場合は上位ビットが消失します
 * 　例) 大きさ4Bitに8Bitのデータ 11111111 を格納すると不足分上位8Bitが消えて 00001111 になります
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine; // 追加

public class SaveManager
{
    public delegate List<BitLayout> SaveInit();

    private static SaveManager _instance;
    private BitContainer _container;
    private string _path;

    // 修正：リストを初期化しておかないと Add で NullReferenceException が出ます
    public static List<SaveInit> saveInits = new List<SaveInit>();

    public static void Add(SaveInit init)
    {
        if (_instance != null)
            throw new Exception("初期化後にセーブ項目を追加することはできません！");
        saveInits.Add(init);
    }

    private static SaveManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // SaveData がない場合はエラーを出すようにする
                if (SaveData.Instance == null)
                {
                    Debug.LogError("SaveData ScriptableObject が Resources フォルダに見つかりません。");
                    return null;
                }
                _instance = new SaveManager(new List<BitLayout>(SaveData.Saves), SaveData.Path);
            }
            return _instance;
        }
    }

    private SaveManager(List<BitLayout> bitLayout, string path)
    {
        saveInits ??= new List<SaveInit>(); // リストが空なら初期化

        foreach (var b in saveInits)
        {
            List<BitLayout> bit = b.Invoke();
            bitLayout.AddRange(bit);
        }
        _container = new BitContainer(bitLayout.ToArray());
        _path = path;

        // 重要：自分自身を Instance に先に登録する
        _instance = this;
        // ファイルが存在する場合のみ読み込む
        // ReadFileの中で Instance を使わないように修正したので、ここで呼んでも安全です
        if (File.Exists(_path)) ReadFile(_path);
    }
    public static T FromByte<T>(byte[] data)
    {
        if (data == null)
            throw new System.Exception(nameof(data));
        Array.Reverse(data);
        Type type = typeof(T);
        
        return typeof(T) switch
        {
            _ when type == typeof(bool) => (T)(object)BitConverter.ToBoolean(data, 0),
            _ when type == typeof(char) => (T)(object)BitConverter.ToChar(data, 0),
            _ when type == typeof(short) => (T)(object)BitConverter.ToInt16(data, 0),
            _ when type == typeof(int) => (T)(object)BitConverter.ToInt32(data, 0),
            _ when type == typeof(long) => (T)(object)BitConverter.ToInt64(data, 0),
            _ when type == typeof(ushort) => (T)(object)BitConverter.ToUInt16(data, 0),
            _ when type == typeof(uint) => (T)(object)BitConverter.ToUInt32(data, 0),
            _ when type == typeof(ulong) => (T)(object)BitConverter.ToUInt64(data, 0),
            _ when type == typeof(float) => (T)(object)BitConverter.ToSingle(data, 0),
            _ when type == typeof(double) => (T)(object)BitConverter.ToDouble(data, 0),
            _ when type == typeof(string) => (T)(object)FromByteS(data),
            _ => throw new NotSupportedException($"{typeof(T)} is not supported"),
        };
    }

    private static byte[] ToByte<T>(T data)
    {
        Type type = typeof(T);
        byte[] bytes = typeof(T) switch
        {
            _ when type == typeof(bool) => BitConverter.GetBytes((bool)(object)data),
            _ when type == typeof(char) => BitConverter.GetBytes((char)(object)data),
            _ when type == typeof(short) => BitConverter.GetBytes((short)(object)data),
            _ when type == typeof(int) => BitConverter.GetBytes((int)(object)data),
            _ when type == typeof(long) => BitConverter.GetBytes((long)(object)data),
            _ when type == typeof(ushort) => BitConverter.GetBytes((ushort)(object)data),
            _ when type == typeof(uint) => BitConverter.GetBytes((uint)(object)data),
            _ when type == typeof(ulong) => BitConverter.GetBytes((ulong)(object)data),
            _ when type == typeof(float) => BitConverter.GetBytes((float)(object)data),
            _ when type == typeof(double) => BitConverter.GetBytes((double)(object)data),
            _ when type == typeof(string) => ToByteS((string)(object)data),
            _ => throw new NotSupportedException($"{typeof(T)} is not supported"),
        };
        Array.Reverse<byte>(bytes);
        return bytes;
    }
    private static byte[] ToByteS(string data) => Encoding.Unicode.GetBytes(data);
    private static string FromByteS(byte[] bytes) => Encoding.Unicode.GetString(bytes, 0, bytes.Length);

    public static int DataSize<T>()
    {
        return Marshal.SizeOf(typeof(T));
    }

public static T Load<T>(string name)
    {
        if (Instance == null) return default;
        int size = (typeof(T) == typeof(string)) ? (Instance._container.GetLength(name) + 7) / 8 : Marshal.SizeOf(typeof(T));
        return FromByte<T>(Instance._container.Read(name, size));
    }

    public static void Save<T>(string name, T data)
    {
        if (Instance == null) return;
        byte[] bytes = ToByte<T>(data);
        Instance._container.Write(name, bytes);
        Instance.WriteFile(Instance._path);
    }
    // --- 修正箇所 2: ReadFile ---
    public void ReadFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            byte[] buffer = new byte[fileStream.Length];
            fileStream.Read(buffer, 0, buffer.Length);

            // 【修正】 Instance._container ではなく _container を直接使う
            _container.ToBytes(buffer);
        }
    }

    // --- 修正箇所 3: WriteFile ---
    public void WriteFile(string filePath)
    {
        using (FileStream fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
        {
            // 【修正】 Instance._container ではなく _container を直接使う
            byte[] data = _container.GetBytes();
            fileStream.Write(data, 0, data.Length);
        }
    }


}
