// Delve - A roguelike computer RPG
// Written in 2026 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

enum KeyCmd
{
  MoveN, MoveS, MoveW, MoveE, MoveNW, MoveNE, MoveSW, MoveSE,
  RunN, RunS, RunW, RunE, UseItem, CastSpell, Chat, Drop,
  Equip, Fire, Force, Inv, Interact, Map, Quit, Save, Search,
  Throw, Debug, Examine, Pickup, Climb, Descend, Messages,
  CharacterSheet, CheatSheetMode, Help, Options, Pass, Nil
}

class KeyMap
{
  Dictionary<char, KeyCmd> _map = [];

  KeyMap(Dictionary<char, KeyCmd> map) => _map = map;

  public static (KeyMap, string) LoadKeyMap()
  {
    DirectoryInfo userDir = Util.UserDir;
    string path = Path.Combine(userDir.FullName, "keymap.txt");
    KeyMap kmap;
    string warning = "";

    if (File.Exists(path))
    {
      Dictionary<char, KeyCmd> map = [];
      List<string> errors = [];

      foreach (string line in File.ReadAllLines(path))
      {
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
          continue;

        int eq = trimmed.LastIndexOf('=');
        if (eq < 1 || eq >= trimmed.Length - 1)
          continue;

        string keyPart = trimmed[..eq];
        string cmdPart = trimmed[(eq + 1)..];

        char key = ParseKey(keyPart);
        if (key == '\0' || !Enum.TryParse<KeyCmd>(cmdPart, out var cmd))
          continue;

        if (map.TryGetValue(key, out var existing))
        {
          errors.Add($"Key '{KeyToString(key)}' is mapped to both {existing} and {cmd}.");
        }
        else
        {
          map[key] = cmd;
        }
      }

      // Check for commands that have no key binding
      HashSet<KeyCmd> allCommands = [.. Enum.GetValues<KeyCmd>().Where(c => c != KeyCmd.Nil)];
      var mappedCommands = new HashSet<KeyCmd>(map.Values);
      List<KeyCmd> missing = [.. allCommands.Except(mappedCommands).OrderBy(c => c.ToString())];
      if (missing.Count > 0)
        errors.Add($"Missing bindings for: {string.Join(", ", missing)}.");

      if (errors.Count > 0)
      {
        warning = "Keymap errors in keymap.txt (default mappings will be used): " + string.Join(" ", errors);
        kmap = Default();
      }
      else
      {
        kmap = new KeyMap(map);
      }
    }
    else
    {
      kmap = Default();
      kmap.Save(path);
    }
      
    kmap._map.Add(Constants.ARROW_N, KeyCmd.MoveN);
    kmap._map.Add(Constants.ARROW_S, KeyCmd.MoveS);
    kmap._map.Add(Constants.ARROW_W, KeyCmd.MoveW);
    kmap._map.Add(Constants.ARROW_E, KeyCmd.MoveE);
    kmap._map.Add(Constants.ARROW_NW, KeyCmd.MoveNW);
    kmap._map.Add(Constants.ARROW_NE, KeyCmd.MoveNE);
    kmap._map.Add(Constants.ARROW_SW, KeyCmd.MoveSW);
    kmap._map.Add(Constants.ARROW_SE, KeyCmd.MoveSE);
    kmap._map.Add(Constants.PASS, KeyCmd.Pass);
    
    return (kmap, warning);
  }

  static char ParseKey(string s)
  {
    if (s.Length == 1)
      return s[0];

    return s.ToLower() switch
    {
      "space" => ' ',
      "comma" => ',',
      "period" => '.',
      "dot" => '.',
      _ => '\0'
    };
  }

  static string KeyToString(char ch)
  {
    return ch switch
    {
      ' ' => "space",
      ',' => "comma",
      '.' => "period",
      _ => ch.ToString()
    };
  }

  void Save(string path)
  {
    var lines = new List<string>
    {
      "# Delve key mappings",
      "# Format: key=Command",
      "# Use 'space', 'comma', 'period' for those keys",
      ""
    };

    foreach (var (key, cmd) in _map.OrderBy(kv => kv.Value.ToString()))
    {
      lines.Add($"{KeyToString(key)}={cmd}");
    }

    File.WriteAllLines(path, lines);
  }

  public static KeyMap Default() => new(new Dictionary<char, KeyCmd>
  {
    ['k'] = KeyCmd.MoveN,
    ['j'] = KeyCmd.MoveS,
    ['h'] = KeyCmd.MoveW,
    ['l'] = KeyCmd.MoveE,
    ['y'] = KeyCmd.MoveNW,
    ['u'] = KeyCmd.MoveNE,
    ['b'] = KeyCmd.MoveSW,
    ['n'] = KeyCmd.MoveSE,

    ['K'] = KeyCmd.RunN,
    ['J'] = KeyCmd.RunS,
    ['H'] = KeyCmd.RunW,
    ['L'] = KeyCmd.RunE,

    ['a'] = KeyCmd.UseItem,
    ['c'] = KeyCmd.CastSpell,
    ['C'] = KeyCmd.Chat,
    ['d'] = KeyCmd.Drop,
    ['e'] = KeyCmd.Equip,
    ['f'] = KeyCmd.Fire,
    ['F'] = KeyCmd.Force,
    ['i'] = KeyCmd.Inv,
    ['o'] = KeyCmd.Interact,
    ['s'] = KeyCmd.Search,
    ['t'] = KeyCmd.Throw,
    ['x'] = KeyCmd.Examine,
    [','] = KeyCmd.Pickup,

    ['<'] = KeyCmd.Climb,
    ['>'] = KeyCmd.Descend,
    ['M'] = KeyCmd.Map,

    ['Q'] = KeyCmd.Quit,
    ['S'] = KeyCmd.Save,
    ['W'] = KeyCmd.Debug,
    ['*'] = KeyCmd.Messages,
    ['@'] = KeyCmd.CharacterSheet,
    ['/'] = KeyCmd.CheatSheetMode,
    ['?'] = KeyCmd.Help,
    ['='] = KeyCmd.Options,
    ['.'] = KeyCmd.Pass,
    [' '] = KeyCmd.Pass,    
  });

  public KeyCmd ToCmd(char ch) => _map.TryGetValue(ch, out KeyCmd cmd) ? cmd : KeyCmd.Nil;

  public string KeyForCmd(KeyCmd cmd)
  {
    foreach (var (key, c) in _map)
    {
      if (c == cmd) 
      {
        string s = KeyToString(key);
        return s switch
        {
          "comma" => ",",
          "period" => ".",
          "space" => " ",
          _ => KeyToString(key),
        };
      }
    }
    return "?";
  }
}