using System;
using System.Collections;
using System.Collections.Generic;

public class BitContainer
{
    private BitLayout[] _data = null;
    private BitArray _bit = null;
    public BitContainer(BitLayout[] data)
    {
        _data = data;
        int size = 0;
        for (int i = 0; i < _data.Length; ++i)
        {
            size += _data[i].length * (int)_data[i].unit;
        }
        _bit = new BitArray(size);
    }

    public int BitLength
    {
        get { return _bit.Length; }
    }
    public int ByteLength
    {
        get { return (_bit.Length + 7) / 8; }
    }

    public byte[] GetBytes() // BitArrayをbyte[]で出力
    {
        byte[] result = new byte[ByteLength];
        BitArray newarray = (BitArray)_bit.Clone();
        if (newarray.Length % 8 != 0) newarray.Length += 8 - (newarray.Length % 8);
        newarray.CopyTo(result, 0);
        return result;
    }
    public void ToBytes(byte[] data) // byte[]を取り込む
    {
        int length = _bit.Length;
        _bit = new BitArray(data);
        _bit.Length = length;
    }
    public int GetLength(string name)
    {
        foreach(BitLayout b in _data)
        {
            if (b.name == name) return b.length * (int)b.unit;
        }
        throw new System.Exception();
    }
    public byte[] Read(string name, int ReturnedByteSize)
    {
        int origin = 0;
        BitLayout? savedata = null;
        for (int i = 0; i < _data.Length; ++i)
        {
            if (_data[i].name == name) { savedata = _data[i]; break; }
            origin += _data[i].length * (int)_data[i].unit;
        }
        if (savedata == null) throw new System.Exception(name + " Does not exist");
        return Get(origin, savedata.Value.length * (int)savedata.Value.unit, ReturnedByteSize);
    }
    public Dictionary<string, byte[]> ReadAll()
    {
        Dictionary<string, byte[]> result = new Dictionary<string, byte[]>();
        int origin = 0;
        for(int i = 0; i < _data.Length; ++i)
        {
            int size = _data[i].length * (int)_data[i].unit;
            byte[] second = Get(origin, size, (size + 7) / 8);
            result.Add(_data[i].name, second);
            origin += size;
        }
        return result;
    }
    public void Write(string name, byte[] data)
    {
        int origin = 0;
        BitLayout? savedata = null;
        for (int i = 0; i < _data.Length; ++i)
        {
            if (_data[i].name == name) { savedata = _data[i]; break; }
            origin += _data[i].length * (int)_data[i].unit;
        }
        if (savedata == null) throw new System.Exception(name + " Does not exist");
        Set(origin, savedata.Value.length * (int)savedata.Value.unit, data);
    }
    public void WriteAll(Dictionary<string, byte[]> data)
    {
        int origin = 0;
        for(int i = 0; i < _data.Length; ++i)
        {
            int size = _data[i].length * (int)_data[i].unit;
            Set(origin, size, data[_data[i].name]);
            origin += size;
        }
    }
    private byte[] Get(int origin, int DataBitSize, int ReturnedByteSize)
    {
        byte[] result = new byte[ReturnedByteSize]; // 結果を格納するバイト配列
        int count = DataBitSize - 1; // ビット数のカウント

        // ReturnedByteSizeに対してビット列を処理
        for (int i = ReturnedByteSize - 1; i >= 0; --i)
        {
            byte currentByte = 0;

            // 8ビット分処理する
            for (int j = 0; j < 8; ++j) // 7から0の順に進める
            {
                if (count < 0) break; // もしビットのカウントがなくなったら終了

                // origin + count で指定された位置のビットを取得
                if (_bit.Get(origin + count))
                {
                    currentByte |= (byte)(1 << j); // ビットをセット
                }
                --count; // カウントを減らす
            }

            result[i] = currentByte; // 現在のバイトを結果に格納
            if (count < 0) break; // 必要なビットが取得できたら終了
        }

        return result; // 結果のバイト配列を返す
    }
    private void Set(int origin, int DataBitSize, byte[] data)
    {
        int count = DataBitSize - 1;
        for (int i = data.Length - 1; i >= 0; --i)
        {
            for (int j = 0; j < 8; ++j)
            {
                if (count < 0) break;
                _bit.Set(origin + count, (data[i] & (1 << j)) != 0);
                --count;
            }
        }
    }

    

}

[Serializable]
public struct BitLayout
{
    public enum Binary
    {
        Bit = 1,
        Byte = 8,
        Char = 16,
        Int = 32,
    }

    public string name;
    public int length;
    public BitLayout.Binary unit;
}
