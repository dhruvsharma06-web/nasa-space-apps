/* ASTRA Academy instructor dashboard: page wiring. All data comes from files the instructor drops in (or the
 * bundled sample); nothing is sent anywhere. Every value from a file is rendered with textContent, never as HTML. */
(function () {
  "use strict";
  const Agg = window.AstraAgg;
  const MISSIONS = window.ASTRA_MISSIONS || {};
  const SVGNS = "http://www.w3.org/2000/svg";
  const COLORS = { Correct: "#30d158", OutOfOrder: "#ffb340", Incorrect: "#ff6961", Ignored: "#5b6474" };
  const MEDAL_COLORS = { Gold: "#ffd60a", Silver: "#c7cdd6", Bronze: "#d08a4e", None: "#5b6474" };

  let attempts = [];          // enriched, all loaded
  let selected = null;        // sessionId shown in the timeline

  const $ = id => document.getElementById(id);
  function el(tag, attrs, ...children) {
    const n = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs || {})) {
      if (k === "class") n.className = v; else if (k === "text") n.textContent = v; else n.setAttribute(k, v);
    }
    for (const c of children) if (c != null) n.append(c instanceof Node ? c : document.createTextNode(String(c)));
    return n;
  }
  function svg(tag, attrs) {
    const n = document.createElementNS(SVGNS, tag);
    for (const [k, v] of Object.entries(attrs || {})) n.setAttribute(k, v);
    return n;
  }
  const secs = ms => (ms / 1000).toFixed(ms < 10000 ? 1 : 0) + " s";
  const pct = x => Math.round(x * 100) + "%";
  const levelName = a => (a.level == null ? a.missionId : "L" + a.level + " " + a.title);

  // ------------------------------------------------------------------ loading
  function add(logs, label) {
    const before = attempts.length;
    const merged = Agg.dedupe(attempts.map(stripEnriched).concat(logs));
    attempts = Agg.enrich(merged, MISSIONS);
    render();
    const added = attempts.length - before;
    return `${added} attempt${added === 1 ? "" : "s"} added from ${label}`;
  }
  const stripEnriched = a => ({ sessionId: a.sessionId, missionId: a.missionId, callsign: a.callsign, source: a.source, result: a.result, events: a.events });

  function notice(text) {
    const n = $("notice");
    n.textContent = text;
    n.hidden = !text;
  }

  async function readFiles(files) {
    const logs = [], errors = [];
    for (const f of files) {
      try {
        const parsed = JSON.parse(await f.text());
        logs.push(...Agg.logsFromJson(parsed, f.name));
      } catch (e) {
        errors.push(`${f.name}: ${e.message.replace(f.name + ": ", "")}`);
      }
    }
    const msg = add(logs, `${files.length} file${files.length === 1 ? "" : "s"}`);
    const unknown = attempts.filter(a => !a.known).length;
    notice([errors.length ? `Skipped ${errors.length}: ${errors.slice(0, 3).join("; ")}${errors.length > 3 ? "…" : ""}` : "",
            unknown ? `${unknown} attempt(s) are for a mission this dashboard doesn't know; they're counted but not scored.` : "",
            errors.length || unknown ? msg : ""].filter(Boolean).join(" "));
  }

  $("file-input").addEventListener("change", e => { readFiles([...e.target.files]); e.target.value = ""; });
  document.querySelector('label[for="file-input"]').addEventListener("keydown", e => {
    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); $("file-input").click(); }
  });
  $("load-sample").addEventListener("click", () => {
    const logs = (window.ASTRA_SAMPLE || []).map((l, i) => Agg.normalizeLog(l, `sample #${i + 1}`));
    add(logs, "the sample");
    notice("");
  });
  $("clear").addEventListener("click", () => { attempts = []; selected = null; notice(""); render(); });
  $("export-csv").addEventListener("click", () => {
    const blob = new Blob([Agg.toCSV(filtered())], { type: "text/csv" });
    const a = el("a", { href: URL.createObjectURL(blob), download: "astra-attempts.csv" });
    document.body.append(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  });
  $("filter-level").addEventListener("change", render);
  $("filter-callsign").addEventListener("change", render);

  let dragDepth = 0;
  window.addEventListener("dragenter", e => { e.preventDefault(); dragDepth++; $("drop").hidden = false; });
  window.addEventListener("dragleave", e => { e.preventDefault(); if (--dragDepth <= 0) { dragDepth = 0; $("drop").hidden = true; } });
  window.addEventListener("dragover", e => e.preventDefault());
  window.addEventListener("drop", e => {
    e.preventDefault(); dragDepth = 0; $("drop").hidden = true;
    const files = [...(e.dataTransfer && e.dataTransfer.files || [])].filter(f => /\.json$/i.test(f.name) || f.type === "application/json");
    if (files.length) readFiles(files); else notice("Only .json session logs can be added.");
  });

  // ------------------------------------------------------------------ filters
  function filtered() {
    const lv = $("filter-level").value, cs = $("filter-callsign").value;
    return attempts.filter(a => (!lv || a.missionId === lv) && (!cs || (a.callsign || "(no callsign)") === cs));
  }

  function fillSelect(select, values, labelOf) {
    const keep = select.value;
    while (select.options.length > 1) select.remove(1);
    for (const v of values) select.append(el("option", { value: v, text: labelOf(v) }));
    select.value = values.includes(keep) ? keep : "";
  }

  // ------------------------------------------------------------------ render
  function render() {
    const has = attempts.length > 0;
    $("empty").hidden = has;
    $("content").hidden = !has;
    $("export-csv").disabled = !has;
    $("clear").disabled = !has;
    if (!has) return;

    const missionIds = [...new Set(attempts.map(a => a.missionId))].sort((a, b) => (levelOf(a) ?? 99) - (levelOf(b) ?? 99));
    fillSelect($("filter-level"), missionIds, id => (MISSIONS[id] ? `L${MISSIONS[id].level} ${MISSIONS[id].title}` : id));
    fillSelect($("filter-callsign"), [...new Set(attempts.map(a => a.callsign || "(no callsign)"))].sort(), v => v);

    const list = filtered();
    const agg = Agg.aggregate(list, MISSIONS);
    renderKpis(agg.overview);
    renderMedalBar($("medal-bar"), agg.overview.medals, true);
    renderLevels(agg.levels);
    renderRank($("missed"), agg.struggles.missed, x => [`L${x.level ?? "?"} · step ${x.order ?? x.stepId}`, x.instruction]);
    renderRank($("order"), agg.struggles.outOfOrder, x => [`L${x.level ?? "?"} · step ${x.order ?? x.stepId}`, x.instruction]);
    renderRank($("wrong"), agg.struggles.wrong, x => [`L${x.level ?? "?"} · ${x.label}`, x.duringOrder ? `pressed during step ${x.duringOrder}: ${x.duringInstruction}` : ""]);
    renderAttempts(list);
  }
  const levelOf = id => (MISSIONS[id] ? MISSIONS[id].level : null);

  function renderKpis(o) {
    const k = $("kpis");
    k.replaceChildren(...[
      [o.attempts, "attempts"], [o.trainees, o.trainees === 1 ? "trainee" : "trainees"],
      [pct(o.passRate), "pass rate"], [o.avgScore, "average score"], [secs(o.avgTimeMs), "average time"],
    ].map(([v, label]) => el("div", { class: "kpi" }, el("div", { class: "v", text: v }), el("div", { class: "k", text: label }))));
  }

  function renderMedalBar(host, medals, labels) {
    const total = Agg.MEDALS.reduce((s, m) => s + (medals[m] || 0), 0) || 1;
    host.replaceChildren(...Agg.MEDALS.filter(m => medals[m]).map(m =>
      el("div", { class: "m-" + m, style: `width:${(medals[m] / total) * 100}%`, title: `${m === "None" ? "No medal" : m}: ${medals[m]}` },
        labels ? `${m === "None" ? "No medal" : m} ${medals[m]}` : null)));
  }

  function passBar(rate) {
    const s = svg("svg", { viewBox: "0 0 200 12", role: "img", "aria-label": "pass rate " + pct(rate) });
    s.append(svg("rect", { x: 0, y: 0, width: 200, height: 12, rx: 6, fill: "rgba(255,255,255,0.09)" }));
    s.append(svg("rect", { x: 0, y: 0, width: Math.max(0, 200 * rate), height: 12, rx: 6, fill: rate >= 0.8 ? "#30d158" : rate >= 0.5 ? "#ffb340" : "#ff6961" }));
    return s;
  }

  function renderLevels(levels) {
    $("level-rows").replaceChildren(...levels.map(l => {
      const mini = el("div", { class: "mini" });
      renderMedalBar(mini, l.medals, false);
      return el("tr", null,
        el("td", null, el("b", { text: l.level == null ? l.missionId : "L" + l.level }), el("span", { text: l.title })),
        el("td", { text: l.attempts }),
        el("td", null, el("div", { class: "bar" }, passBar(l.passRate), el("span", { text: `${pct(l.passRate)} (${l.passed}/${l.attempts})` }))),
        el("td", { text: l.avgScore }),
        el("td", { text: secs(l.avgTimeMs) }),
        el("td", null, mini));
    }));
  }

  function renderRank(host, items, text) {
    if (!items.length) { host.replaceChildren(el("li", { class: "none", text: "None in these attempts." })); return; }
    host.replaceChildren(...items.slice(0, 6).map(x => {
      const [title, detail] = text(x);
      return el("li", null, el("span", { class: "n", text: x.count }),
        el("div", null, el("div", { class: "t", text: title }), detail ? el("div", { class: "d", text: detail }) : null));
    }));
  }

  function medalChip(m) {
    return el("span", { class: "medal" }, el("i", { style: `background:${MEDAL_COLORS[m]}` }), m === "None" ? "—" : m);
  }

  function renderAttempts(list) {
    const rows = [...list].sort((a, b) => (a.callsign || "").localeCompare(b.callsign || "") || (a.level ?? 99) - (b.level ?? 99) || a.sessionId.localeCompare(b.sessionId));
    $("attempt-rows").replaceChildren(...rows.map(a => {
      const mistakes = a.result.omittedCriticalSteps.length + a.result.sequenceErrors.length + a.result.incorrectActions.length;
      const tr = el("tr", { tabindex: 0, class: a.sessionId === selected ? "sel" : "" },
        el("td", { text: a.callsign || "(no callsign)" }),
        el("td", { text: levelName(a) }),
        el("td", null, el("span", { class: "pill " + (a.result.passed ? "pass" : "fail"), text: a.result.passed ? "PASSED" : "NOT PASSED" })),
        el("td", { text: a.score == null ? "—" : a.score }),
        el("td", null, medalChip(a.medal)),
        el("td", { text: secs(a.result.completionTimeMs) }),
        el("td", { text: mistakes }));
      const open = () => { selected = a.sessionId; renderAttempts(list); renderTimeline(a); };
      tr.addEventListener("click", open);
      tr.addEventListener("keydown", e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); open(); } });
      return tr;
    }));
    const current = list.find(a => a.sessionId === selected);
    if (current) renderTimeline(current); else $("timeline").hidden = true;
  }

  // ------------------------------------------------------------------ timeline
  function describe(t, mission) {
    const step = mission && mission.steps.find(s => s.stepId === (t.stepId || t.currentStepId));
    switch (t.outcome) {
      case "Correct": return `Step ${step ? step.order : "?"}: ${Agg.humanizeId(t.targetId)}`;
      case "OutOfOrder": return `Too early: ${Agg.humanizeId(t.targetId)} (step ${step ? step.order : "?"})`;
      case "Incorrect": return `Wrong control: ${Agg.humanizeId(t.targetId)}` + (step ? ` during step ${step.order}` : "");
      default: return `${t.event.replace(/_/g, " ")}: ${Agg.humanizeId(t.targetId)} (no effect)`;
    }
  }

  function renderTimeline(a) {
    const mission = MISSIONS[a.missionId];
    $("timeline").hidden = false;
    $("timeline-title").textContent = `${a.callsign || "(no callsign)"} · ${levelName(a)} · ${a.result.passed ? "passed" : "not passed"} · ${secs(a.result.completionTimeMs)}`;

    const W = 1000, H = 120, pad = 30, axisY = 70;
    const end = Math.max(1000, a.result.completionTimeMs, ...a.timeline.map(t => t.timestampMs));
    const x = ms => pad + (W - 2 * pad) * (ms / end);
    const s = svg("svg", { viewBox: `0 0 ${W} ${H}`, role: "img", "aria-label": "Event timeline" });
    s.append(svg("line", { x1: pad, x2: W - pad, y1: axisY, y2: axisY, stroke: "rgba(255,255,255,0.25)", "stroke-width": 2 }));
    const tick = Math.max(5000, Math.ceil(end / 8 / 5000) * 5000);
    for (let ms = 0; ms <= end; ms += tick) {
      s.append(svg("line", { x1: x(ms), x2: x(ms), y1: axisY - 4, y2: axisY + 4, stroke: "rgba(255,255,255,0.35)" }));
      const label = svg("text", { x: x(ms), y: axisY + 24, fill: "#9aa6b8", "font-size": 14, "text-anchor": "middle" });
      label.textContent = Math.round(ms / 1000) + " s";
      s.append(label);
    }
    a.timeline.forEach((t, i) => {
      const off = t.outcome === "Correct" ? 0 : -22;
      const c = svg("circle", { cx: x(t.timestampMs), cy: axisY + off, r: t.outcome === "Correct" ? 8 : 9, fill: COLORS[t.outcome], stroke: "#070b14", "stroke-width": 2 });
      const title = svg("title");
      title.textContent = `${secs(t.timestampMs)} · ${describe(t, mission)}`;
      c.append(title);
      s.append(c);
      if (i === a.timeline.length - 1 && t.event === "status_reported") {
        const done = svg("text", { x: x(t.timestampMs), y: axisY - 16, fill: "#f2f5fa", "font-size": 14, "text-anchor": "end" });
        done.textContent = "report";
        s.append(done);
      }
    });
    $("timeline-chart").replaceChildren(s);

    $("timeline-events").replaceChildren(...a.timeline.map(t =>
      el("li", null, el("span", { class: "ts", text: secs(t.timestampMs) }), el("span", { class: "dot", style: `background:${COLORS[t.outcome]}` }), el("span", { text: describe(t, mission) }))));

    const lines = a.lines.map(l => el("li", null, el("span", { text: l.label }),
      el("span", { class: l.points < 0 ? "neg" : l.points > 0 ? "pos" : "", text: l.points === 0 ? "0" : (l.points > 0 ? "+" : "") + l.points })));
    lines.push(el("li", { class: "total" }, el("span", { text: `Score · ${a.medal === "None" ? "no medal" : a.medal}` }), el("span", { text: a.score == null ? "—" : a.score })));
    $("timeline-lines").replaceChildren(...lines);
  }

  render();
  if (location.hash === "#sample") $("load-sample").click();   // index.html#sample opens with the sample loaded
})();
