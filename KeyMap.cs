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
    [' '] = KeyCmd.Pass,
    ['.'] = KeyCmd.Pass,
  });

  public KeyCmd ToCmd(char ch) => _map.TryGetValue(ch, out KeyCmd cmd) ? cmd : KeyCmd.Nil;
}