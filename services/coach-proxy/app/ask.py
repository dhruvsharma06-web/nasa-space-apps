"""Deterministic handling of free-form trainee questions (advisory only).

The question is untrusted data. `screen` refuses unsafe requests before any model
call; `offline_answer` is the keyword fallback used in mock mode and when the model
output is rejected. Answers come only from the mission JSON.
"""
import re

REDIRECT = "I can help with this mission and your training."
SAFETY_REFUSAL = (
    "I can't help with that. I can't share Qualification answers or scores; "
    "I can explain the current step or the procedure."
)

_UNSAFE = re.compile(
    r"ignore (?:all |your |the |any )?(?:previous |prior |above )?(?:rules|instructions|prompt)"
    r"|disregard|forget (?:your|the|all)|system prompt|your (?:rules|instructions|prompt)"
    r"|jailbreak|developer mode|pretend (?:to be|you)|act as|you are now"
    r"|\b(?:score|scores|scoring|grade|pass|passed|fail|failed|points)\b.*\b(?:change|set|raise|increase|give|tell|reveal|show|what)"
    r"|\b(?:change|set|raise|increase|give|tell|reveal|show|what|my)\b.*\b(?:score|scores|grade|points|pass/fail)\b"
    r"|qualification.*(?:answer|solution|cheat|steps|key)|(?:answer|solution|cheat|key).*qualification"
    r"|\bcheat\b|\bhack\b|bypass|api key|password|secret|override",
    re.I,
)
_STOP = frozenset(
    "the and for with that this from your you are was were will then when into onto have has "
    "not but can all any one two use set what how why does where which who should step steps "
    "please tell about".split()
)
_TOPIC = frozenset(
    "mission level step steps control controls panel switch dial toggle button lever training "
    "practice briefing demonstration qualification debrief phase phases astra station "
    "instructor checklist procedure hint study tip tips learn remember prepare ready "
    "matter matters important stuck level levels next previous first last done finish complete safe safety".split()
)
_STEPS_INTENT = re.compile(r"\bnext\b|\bwhat now\b|\bwhat do i do\b|\bwhat should i do\b|\bstuck\b|\bhelp\b", re.I)
_WHY_INTENT = re.compile(r"\bwhy\b|\bmatters?\b|\bpurpose\b|\bimportant\b|\breason\b", re.I)
_WHERE_INTENT = re.compile(r"\bwhere\b|\bwhich\b|\bcontrol\b|\bwhat does\b|\bfind\b|\blocate\b|\bhow (?:do|to)\b", re.I)
_LEVEL_INTENT = re.compile(r"\b(?:what is|about|this) (?:this )?(?:level|mission|lesson)\b|\bwhat.*(?:level|mission)\b", re.I)
_STUDY_INTENT = re.compile(r"\bstudy\b|\btips?\b|\badvice\b|\bimprove\b|\bphases?\b|\bqualif", re.I)

STUDY_TIP = (
    "Practice each step slowly, read the checklist, and say the control label aloud. "
    "Briefing, Demonstration, Practice, Qualification, Debrief: use Practice to build the sequence."
)


def screen(question: str) -> str | None:
    """Return a refusal if the question is unsafe or an injection attempt, else None."""
    return SAFETY_REFUSAL if _UNSAFE.search(question) else None


def _words(text: str) -> set[str]:
    return {w for w in re.findall(r"[a-z]{3,}", text.lower()) if w not in _STOP}


def _mission_vocab(mission: dict) -> set[str]:
    vocab = _words(mission.get("title") or "")
    for s in mission.get("steps", []):
        vocab |= _words(s.get("instruction") or "") | _words(s.get("hint") or "")
    return vocab


def is_on_topic_question(question: str, mission: dict) -> bool:
    raw = set(re.findall(r"[a-z]{3,}", question.lower()))
    return bool(_words(question) & _mission_vocab(mission)) or bool(raw & _TOPIC)


def _trim(text: str, limit: int = 40) -> str:
    words = text.split()
    if len(words) <= limit:
        return text
    head = " ".join(words[:limit])
    cut = max(head.rfind(". "), head.rfind("."))
    return head[: cut + 1] if cut > 0 else head.rstrip(",;:") + "."


def offline_answer(mission: dict, step: dict | None, question: str) -> str:
    """Keyword fallback grounded in the mission steps and hints."""
    refusal = screen(question)
    if refusal:
        return refusal
    steps = sorted(mission.get("steps", []), key=lambda s: s.get("order") or 0)
    if _LEVEL_INTENT.search(question):
        title = mission.get("title") or "this mission"
        return _trim(f"This level is {title}. It has {len(steps)} steps; follow the checklist in order.")
    if not is_on_topic_question(question, mission):
        return REDIRECT
    # a question that names another step's wording gets that step's hint
    qw = _words(question)
    named = None
    best = 0
    for s in steps:
        score = len(qw & _words(s.get("instruction") or ""))
        if score > best:
            best, named = score, s
    if _STUDY_INTENT.search(question) and not best:
        return _trim(STUDY_TIP)
    if step is None:
        return "All steps are complete."
    if _WHY_INTENT.search(question):
        return _trim(step.get("hint") or step.get("instruction") or REDIRECT)
    if _STEPS_INTENT.search(question) and not _WHERE_INTENT.search(question):
        nxt = next((s for s in steps if (s.get("order") or 0) > (step.get("order") or 0)), None)
        if nxt and re.search(r"\bnext\b", question, re.I):
            return _trim("After this step: " + (nxt.get("instruction") or ""))
        return _trim(step.get("instruction") or "")
    target = named if named and _WHERE_INTENT.search(question) else step
    return _trim(target.get("hint") or target.get("instruction") or REDIRECT)
