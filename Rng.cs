// Delve - A roguelike computer RPG
// Written in 2025 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

// A translation of the xoshiro256** algorithm from
//   https://en.wikipedia.org/wiki/Xorshift

// The only reason I'm bothering implementing my own is because I want to 
// be able to serialize the internal state of the rng in save game files
// and restore it on load

namespace Yarl2;

public class Rng
{
  public int InitialSeed { get; init; }
  readonly ulong[] s = new ulong[4];

  public static Rng FromState(ulong[] state, int initialSeed) => new()
  {
    State = state,
    InitialSeed = initialSeed
  };

  protected Rng() { }

  public Rng(int seed)
  {
    InitialSeed = seed;

    ulong ulSeed = (ulong)(uint)seed;
    s[0] = ulSeed;
    s[1] = ulSeed << 16;
    s[2] = ulSeed << 32;
    s[3] = ulSeed << 48;
  }

  public ulong[] State
  {
    get => [.. s];
    set
    {
      if (value.Length != 4)
        throw new ArgumentException("State must be exactly 4 values");
      Array.Copy(value, s, 4);
    }
  }

  static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));

  ulong _next()
  {
    ulong result = RotateLeft(s[1] * 5, 7) * 9;
    ulong t = s[1] << 17;

    s[2] ^= s[0];
    s[3] ^= s[1];
    s[1] ^= s[2];
    s[0] ^= s[3];

    s[2] ^= t;
    s[3] = RotateLeft(s[3], 45);

    return result;
  }

  public int Next() => (int)(_next() >> 33);

  public int Next(int maxValue)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxValue);

    // Mask and threshold to get a positive number to avoid bias
    ulong mask = (1UL << 32) - 1;
    ulong threshold = mask - (mask % (ulong)maxValue);

    while (true)
    {
      ulong result = _next() & mask;
      if (result < threshold)
        return (int)(result % (ulong)maxValue);
    }
  }

  public int Next(int minValue, int maxValue)
  {
    ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

    ulong range = (ulong)(maxValue - minValue);

    return minValue + Next((int)range);
  }

  public double NextDouble()
  {
    // Get 53 random bits for the mantissa (standard double precision)
    ulong value = _next() >> 11;

    return value * (1.0 / (1UL << 53));
  }
}

// This is cribbed and tweaked from https://adrianb.io/2014/08/09/perlinnoise.html
// If I end up using perlin noise for a few things, I might bit the bullet and 
// actually install a noise package from nuget
public class PerlinNoise
{
  private readonly int[] perm;

  public PerlinNoise(Rng rng)
  {
    int[] p = new int[256];
    for (int i = 0; i < 256; i++)
      p[i] = i;

    for (int i = 255; i > 0; i--)
    {
      int j = rng.Next(i + 1);
      (p[i], p[j]) = (p[j], p[i]);
    }

    perm = new int[512];
    for (int i = 0; i < 512; i++)
      perm[i] = p[i % 256];
  }

  static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
  static float Lerp(float a, float b, float t) => a + t * (b - a);

  static float Grad(int hash, float x, float y, float z) => (hash & 0xF) switch
  {
    0x0 => x + y,
    0x1 => -x + y,
    0x2 => x - y,
    0x3 => -x - y,
    0x4 => x + z,
    0x5 => -x + z,
    0x6 => x - z,
    0x7 => -x - z,
    0x8 => y + z,
    0x9 => -y + z,
    0xA => y - z,
    0xB => -y - z,
    0xC => y + x,
    0xD => -y + z,
    0xE => y - x,
    0xF => -y - z,
    _ => 0,// never happens
  };

  public float Noise(float x, float y, float z)
  {
    int xi = (int)x & 255;
    int yi = (int)y & 255;
    int zi = (int)z & 255;
    float xf = x - (int)x;
    float yf = y - (int)y;
    float zf = z - (int)z;
    float u = Fade(xf);
    float v = Fade(yf);
    float w = Fade(zf);

    int aaa, aba, aab, abb, baa, bba, bab, bbb;
    aaa = perm[perm[perm[xi] + yi    ] + zi   ];
    aba = perm[perm[perm[xi] + yi + 1] + zi   ];
    aab = perm[perm[perm[xi] + yi    ] + zi + 1];
    abb = perm[perm[perm[xi] + yi + 1] + zi + 1];
    baa = perm[perm[perm[xi + 1] + yi    ] + zi   ];
    bba = perm[perm[perm[xi + 1] + zi + 1] + zi   ];
    bab = perm[perm[perm[xi + 1] + yi    ] + zi + 1];
    bbb = perm[perm[perm[xi + 1] + yi + 1] + zi + 1];

    float x1 = Lerp(Grad(aaa, xf, yf, zf), Grad(baa, xf - 1, yf, zf), u);
    float x2 = Lerp(Grad(aba, xf, yf - 1, zf), Grad(bba, xf - 1, yf - 1, zf), u);
    float y1 = Lerp(x1, x2, v);

    x1 = Lerp(Grad(aab, xf, yf, zf - 1), Grad(bab, xf - 1, yf, zf - 1), u);
    x2 = Lerp(Grad(abb, xf, yf - 1, zf - 1), Grad(bbb, xf - 1, yf - 1, zf - 1), u);
    float y2 = Lerp(x1, x2, v);

    return (Lerp(y1, y2, w) + 1) / 2;
  }
}