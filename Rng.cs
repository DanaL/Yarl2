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
  readonly ulong[] s = new ulong[4];

  public static Rng FromState(ulong[] state) => new()
  {
    State = state
  };

  protected Rng() { }

  public Rng(int seed)
  {
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