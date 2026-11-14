# How ASTRA Academy scores an attempt

Every Qualification attempt gets two separate results:

1. **Pass or not pass.** The mission scorer decides this (CONTRACT §5). You pass only if you complete every **critical** step, and nothing can override that: not speed, not the score, not the AI coach.
2. **A score from 0 to 100 and a medal.** These come from the pass result plus what went wrong, and are explained line by line in the debrief. Code: `Astra.Core.Assessment.ResultRating`.

Both are deterministic: the same actions always give the same pass result, score and medal.

## The score

Start at **100**, then:

| What happened | Points |
|---|---|
| A critical step was missed | **−25** each |
| A step was done out of order | **−10** each |
| A wrong control was used (e.g. the other bus's breaker) | **−8** each |
| Time over par (par = 8 s × number of steps) | **−1** per full 5 s over, at most **−15** |
| "Swift": under 70 % of par with no mistakes | **+5** |

Then three limits apply:
- The score never goes above **100**.
- If the attempt did **not pass**, the score is capped at **59**.
- The score never goes below **0**.

The debrief lists every line that changed the score, and the score always equals 100 plus those lines.

## The medal

| Medal | When |
|---|---|
| **Gold** | passed, score ≥ 90 |
| **Silver** | passed, score ≥ 75 |
| **Bronze** | passed, score below 75 |
| none | not passed |

## Example

L4, seven steps, so par is 56 s. The cadet uses the BUS A dial by mistake, then finishes correctly in 66 s:

| Line | Points |
|---|---|
| Start | 100 |
| Wrong control: POWER BUS A | −8 |
| Time: 10 s over par of 56 s | −2 |
| **Score** | **90, Gold** |

## Where the score is used

- **Debrief:** the score, the medal and the lines.
- **Hub:** each level's best score and medal; the **Academy score** is the sum of the best scores.
- **Certificate and instructor dashboard.**

The coach (offline or AI) only gives advice; it never changes the pass result, the score or the medal.
