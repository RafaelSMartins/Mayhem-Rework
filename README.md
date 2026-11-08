<a id="readme-top"></a>
![Mayhem Logo](MAYHEM.png)

<div align="center">
  <h3 align="center">MAYHEM — Rework</h3>

  <p align="center">
    A 2D pseudo platformer-fighting game, originally made for the DJD1 class at Universidade Lusófona's Videogames BA — now reworked into an endless, replayable run.
    <br />
    <a href="https://github.com/RafaelSMartins/Mayhem-Rework"><strong>Repository »</strong></a>
    ·
    <a href="https://txgerdev.itch.io/mayhem"><strong>Original on itch.io »</strong></a>
  </p>
</div>

## About The Project

“We are the All-Singing, All-dancing crap of this world”

Taking place in the same universe as the film Fight Club (1999), you play as William Richter, a nobody who struggles to afford a place in society.
He works a café job he hates — demanding customers, a pushy boss, the same orders over and over — and fights his addictions by taking late-night walks to clear his head.
On one of those walks he finds the bar where the Fight Club meets, and starts going there to let out his anger and stress on people who share the same problems. As the story goes on, William gets pulled deeper into Project Mayhem and its plan to tear society down and start over.

![Mayhem Screenshot](2dplatformer_05.png)

![Mayhem Screenshot](2dplatformer_06.png)

![Mayhem Screenshot](2dplatformer_03.png)

![Mayhem Screenshot](2dplatformer_04.png)

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## What's New in the Rework

The original was a linear sequence of levels. The rework turns it into an endless run that remembers your money and health from one night to the next:

**Café shift → Night hub → Fight Club → Hospital → next day**

- **Café shift (60 s):** serve orders to earn money. Wrong orders and customers who give up cost you, and the stress you build up becomes your starting **Fury** for the fight.
- **Night hub:** spend your earnings on patch-ups and first aid kits before the fight, or skip work entirely and go in with full rage but no pay.
- **Fight Club:** several rounds of enemies. Each night brings more of them, and they hit harder and more often.
- **Hospital:** pay for treatment and recover a bit overnight. Then it's back to the café, until you go down.

### Game modes

| Mode | Description |
|------|-------------|
| **Run** | Café by day, Fight Club by night. Manage your money and your health. Your record is the most nights survived. |
| **Cozy Café** | Just the café: no clock pressure, no fines, more patient customers. Your record is your best shift. |
| **Fight Only** | Endless waves of fighters, each one tougher, with a free first aid kit every 3 waves. Your record is the most waves survived. |

Other additions: a reworked combat system (light/heavy attacks, block, dodge, Fury mode, hit-stop and combat FX), per-scene post-processing, a see-through shader for sprites behind scenery, a unified input setup (keyboard + gamepad) and a pause menu.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Controls

| Action | Keyboard / Mouse | Gamepad |
|--------|------------------|---------|
| Move | WASD / Arrow keys | Left stick / D-pad |
| Attack | J / Left mouse | West (□ / X) |
| Heavy attack | K | North (△ / Y) |
| Block | L / Right mouse | Right shoulder |
| Dodge | Left Shift / H | East (○ / B) |
| Fury | Space | Left shoulder |
| Use first aid kit | U | South (✕ / A) |
| Interact / pick up / deliver | Enter / J / Space | South / West |
| Drop toast | Q | Left trigger |
| Drop coffee | E | Right trigger |
| Pause | Esc | Start |

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Getting Started (Development)

1. Install **Unity 6000.0.39f1** (Unity 6) through Unity Hub.
2. Clone the repository:
   ```bash
   git clone https://github.com/RafaelSMartins/Mayhem-Rework.git
   ```
3. Open the folder in Unity Hub and open the `TitleScreen` scene.

Built with the Universal Render Pipeline (2D), the Input System and TextMeshPro.

Build order: `TitleScreen` → `CoffeeVideo` → `CoffeeShop2` → `NightHub` → `Level1` → `Hospital`.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Contributors

Original team:

- Simão Campaniço - [simaocampanicodev](https://github.com/simaocampanicodev)
- João Ferreira - [KR1PT1CS](https://github.com/KR1PT1CS)
- Dinis Barroso - [DinisB](https://github.com/DinisB)
- Rafael Martins - [RafaelSMartins](https://github.com/RafaelSMartins)
- Ivan Batista - [@whos_batixta](https://www.instagram.com/whos_batixta)

Rework by Rafael Martins - [RafaelSMartins](https://github.com/RafaelSMartins)

Feedback is always appreciated! If you have a suggestion, open an issue, and don't forget to give the project a star.

<p align="right">(<a href="#readme-top">back to top</a>)</p>
