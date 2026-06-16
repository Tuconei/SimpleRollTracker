# Simple Roll Tracker

Simple Roll Tracker is a Dalamud plugin for Final Fantasy XIV that captures `/random` rolls from chat and helps you run quick player-made dice games, giveaways, contests, and mini-events.

Open the tracker with `/rolls`, start collecting rolls, choose the win condition, and let the plugin keep track of players, roll values, winners, saved rounds, and player history.

## Features

- Automatically records nearby `/random` rolls from chat.
- Shows a live sortable roll table with time, player, roll, and delete controls.
- Supports high-roll and low-roll winner modes.
- Supports target-number games with exact-match or closest-to-target winners.
- Supports threshold games where rolls above or below a value qualify.
- Can lock tracking to your current target.
- Can limit entries to one roll per person.
- Can automatically clear old rolls after a chosen number of minutes.
- Saves completed rounds with winner and roll details.
- Provides player history across saved rounds, including rolls, wins, high, low, and average.
- Tracks lifetime analytics for total rolls, highest roll, lowest roll, and average roll.
- Highlights configurable "funny numbers" such as `69`, `420`, `777`, and `999`.
- Copies customizable winner announcements with `{winner}` and `{roll}` placeholders.
- Lets you click player names in the tracker to target them in-game when they are nearby.

## Chat Commands

| Command | Description |
| --- | --- |
| `/rolls` | Opens or closes the Simple Roll Tracker window. |
| `/startrolls` | Starts recording rolls. Useful in macros. |
| `/stoprolls` | Stops recording rolls. Useful in macros. |
| `/clearrolls` | Clears the current roll list. |

## Basic Usage

1. Open the plugin with `/rolls`.
2. Make sure capturing is enabled. The top button will show `STOP CAPTURING` while the plugin is actively recording rolls.
3. Ask players to use `/random`.
4. Watch rolls appear in the table.
5. Choose the game mode:
   - `Mode: HIGHEST Wins`
   - `Mode: LOWEST Wins`
   - `Target Mode`
   - `Threshold`
6. Use `Copy Announcement` to copy the winner message, or manually announce the result.
7. Click `New Round` to save the current round and clear the active roll list.

## Game Modes

### Highest Roll Wins

This is the default style of roll contest. The highest recorded roll is shown as the winner.

Example:

> "Highest roll wins the mount. Everyone type `/random` once."

Recommended settings:

- Target Mode: off
- Mode: `HIGHEST Wins`
- Allow only 1 roll per person: on

### Lowest Roll Wins

Click the mode button until it shows `Mode: LOWEST Wins`. The lowest recorded roll becomes the winner.

Example:

> "Lowest roll gets to pick the next map."

Recommended settings:

- Target Mode: off
- Mode: `LOWEST Wins`
- Allow only 1 roll per person: on

### Exact Target Number

Enable `Target Mode`, add one or more target numbers, and leave `Closest Wins` off. A player wins when their roll exactly matches one of the target values.

Example:

> "First person to roll `777` wins."

Recommended settings:

- Target Mode: on
- Target number: `777`
- Closest Wins: off

You can add multiple target numbers for games such as:

> "Roll `69`, `420`, or `999` to win a bonus prize."

### Closest to Target

Enable `Target Mode`, add a target number, and enable `Closest Wins`. The plugin chooses the roll with the smallest distance from the target.

Example:

> "Closest roll to `500` wins."

Recommended settings:

- Target Mode: on
- Target number: `500`
- Closest Wins: on
- Allow only 1 roll per person: on

### Threshold Qualifier

Enable `Target Mode`, enable `Threshold`, choose a value, then choose whether rolls must be above or below that value. Instead of picking one winner, the plugin lists everyone who qualifies.

Examples:

> "Everyone who rolls `900` or higher gets a prize."

> "Anyone who rolls `100` or lower has to do the next pull."

Recommended settings:

- Target Mode: on
- Threshold: on
- Threshold value: `900`
- Above/Below: choose the direction for the event

## Useful Controls

### Clear List

Clears the current active roll list without deleting saved rounds.

### Clear > X Mins

Automatically removes active rolls older than the chosen number of minutes. Set it to `0` to disable automatic clearing.

### Lock to Current Target

Locks the tracker so it only accepts rolls from your currently selected target. This is useful for one-on-one games or when you only want to track a specific player.

### Allow Only 1 Roll Per Person

Ignores additional rolls from players who already have an active roll in the current round.

### Funny Numbers

Adds cyan highlighting for selected numbers. Defaults include:

- `0`
- `1`
- `69`
- `420`
- `777`
- `999`

These numbers are highlights only unless they are also part of your target-number rules.

### Announcement Template

Customize the copied winner message with placeholders:

- `{winner}`: winner's character name
- `{roll}`: winning roll value

Example template:

```text
Congratulations to {winner} for rolling a {roll}!
```

## Saved Rounds and Player History

Click `New Round` to save the current roll list as a completed round. Saved rounds store:

- round number
- time saved
- winner or qualifier summary
- all rolls from that round

The `Player History` section lets you search for a player and review their performance across saved rounds, including:

- saved rounds played
- total rolls
- wins
- highest roll
- lowest roll
- average roll

## Example Events

### Giveaway Roll

Players roll once, highest roll wins the prize.

Settings:

- Highest Wins
- Allow only 1 roll per person

### Reverse Giveaway

Players roll once, lowest roll wins.

Settings:

- Lowest Wins
- Allow only 1 roll per person

### Jackpot Number

Players try to hit an exact number, such as `777`.

Settings:

- Target Mode
- Add target `777`
- Closest Wins off

### Closest to the Host

The host chooses a secret or public number, and the closest roll wins.

Settings:

- Target Mode
- Add target number, such as `500`
- Closest Wins on

### Lucky List

Several numbers count as lucky numbers.

Settings:

- Target Mode
- Add targets such as `69`, `420`, and `999`
- Closest Wins off

### Qualification Round

Everyone above or below a threshold qualifies for the next round.

Settings:

- Target Mode
- Threshold on
- Choose Above or Below
- Set the threshold value

### One-on-One Duel

Track rolls from only one targeted player.

Settings:

- Select the player in-game
- Click `Lock to Current Target`
- Choose High, Low, Target, or Threshold rules

## Notes

- The plugin records chat messages containing FFXIV random-roll output.
- Rolls are saved in the plugin configuration, along with saved rounds and lifetime analytics.
- The plugin ignores echo chat messages and does not record while capturing is stopped.
- Player targeting from the roll table works when the player is nearby and visible to the object table.

## License

This project is licensed under the terms in [LICENSE.md](LICENSE.md).
