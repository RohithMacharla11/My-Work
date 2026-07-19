# Candidate Hiring Workflow — Status, Visibility & Assignment Logic

## 1. Roles & Hierarchy

Low → High:

1. **Tech Panel members**
2. **Management Panel members**
3. **Onshore Panel members**
4. **HR Panel members**
5. **HR Admin** — full permissions everywhere (can see/edit at every stage, no restrictions)

General rule: **higher-hierarchy roles can always see a candidate that a lower-hierarchy role currently holds; lower-hierarchy roles lose visibility once a candidate has moved past their stage.**

---

## 2. Full Status List

| # | Status | Meaning |
|---|--------|---------|
| 1 | Rejected | Candidate eliminated (can happen at Prescreening, Tech Round 2, Management, or Onshore) |
| 2 | Pending with Tech Round 1 | Cleared prescreening, waiting for a Tech Panel member |
| 3 | Pending with Tech Round 2 or Management Round | Tech Round 1 result submitted; Tech Round 2 is optional/skippable |
| 4 | Pending with Management Round | Tech Round 2 selected, or Management skipped Tech Round 2 |
| 5 | Pending with Onshore Round or HR Round | Management selected; Onshore is optional |
| 6 | Pending with Onshore | HR assigned to an Onshore panel member |
| 7 | Pending with HR | Onshore selected candidate, OR HR skipped Onshore |

---

## 3. Stage-by-Stage Flow

### Stage 0 — Prescreening
- **Selected** → status = *Pending with Tech Round 1*
- **Not selected** → status = *Rejected*

### Stage 1 — Tech Round 1
**Visibility:** Tech Panel, Management Panel, Onshore Panel, HR Panel, HR Admin can all see the candidate exists — but everyone except HR Admin only sees **prescreening-level info** until a Tech Round 1 result is submitted.

**Assignment:**
- Any Tech Panel member can assign the candidate to themselves.
- Once assigned: other Tech Panel members see a 🔒 lock (cannot open/edit) but keep an "open" view limited to prescreening info only.
- Higher-hierarchy roles also stay locked to prescreening-level info until the result is submitted.

**Result:** Tech Round 1 is **not** an elimination round — outcome is just "Recommended" / "Not Recommended" (a note only). Candidate always proceeds.
→ status becomes *Pending with Tech Round 2 or Management Round*

### Stage 2 — Tech Round 2 or Management Round (candidate can go either way)
- **Any Tech Panel member** can assign themselves for Tech Round 2 → locks it (🔒) for all other Tech Panel members, "open" view only, same pattern as above.
- **OR any Management Panel member** can directly assign themselves here, **skipping Tech Round 2** → system shows a warning ("You're skipping Tech Round 2") → candidate assigned to that manager → status = *Pending with Management Round*.

**Tech Round 2 outcomes (if not skipped):**
- **Rejected** → status = *Rejected*, stops.
- **Selected** → candidate is **fully removed** from all Tech Panel members' view (not just locked) → status = *Pending with Management Round*.

### Stage 3 — Management Round
**Also reachable directly from Stage 1 or Stage 2** if a manager grabs the candidate early (with the "skipping Tech Round 2" warning).

**Lock behavior once a manager assigns themselves:**
- Locked (🔒, no access) for: all other Management Panel members, all Tech Panel members.
- Onshore, HR, HR Admin: can **view** (read-only) everything up to the round *before* Management, but not the live Management round details.
- Only the assigned manager can edit / give feedback.

**Outcomes:**
- **Rejected** → status = *Rejected*. Removed from the manager's round view. Tech Panel (lower hierarchy) loses all visibility. Only Onshore, HR, and HR Admin can still see the rejected status.
- **Selected** → candidate removed from both Management Panel dashboards and Tech Panel dashboards. Visible to Onshore, HR, HR Admin as → status = *Pending with Onshore Round or HR Round*.

### Stage 4 — Onshore or HR
- Onshore Panel members **cannot self-assign**.
- HR sees candidates in two possible statuses: *Pending with Onshore or HR* and *Pending with HR*.
- To assign, HR clicks "assign" → a **modal** opens with a dropdown:
  - **Option A – Onshore:** HR picks a specific Onshore panel member from the dropdown → candidate assigned & locked to that person → status = *Pending with Onshore*.
  - **Option B – HR (skip Onshore):** system shows a skip warning → HR assigns an HR person directly → status = *Pending with HR* (Onshore round bypassed).

### Stage 5 — Onshore Round (if not skipped)
- **Rejected** → status = *Rejected*.
- **Selected** → status = *Pending with HR*.

### Stage 6 — HR Round
- An HR person can self-assign an open "Pending with HR" candidate and proceed with feedback, same pattern as earlier rounds.

---

## 4. Unassignment Rules

| Round unassigned | Result |
|---|---|
| Tech Round 1 | Candidate reopens to **all** Tech Panel members. |
| Tech Round 2 | Candidate reopens to **all** Tech Panel members. |
| Management Round | If Tech Round 2 was **completed** first → candidate stays visible only to Management Panel (reopens for reassignment there). If Tech Round 2 was **skipped** → candidate goes back to being available to **both** Tech Panel (to run Tech Round 2) and Management Panel. |
| Onshore Round | Since Onshore members can't self-assign anyway, an unassign just clears the assignment — it needs HR to reassign via the modal again. |
| HR Round | HR recruiter can unassign anyone. See two sub-cases below. |

**HR-round unassign sub-cases (as described — see open question #3 below):**
- If the candidate's Onshore round was **not skipped** (they went Onshore → selected → HR), and HR now unassigns the HR-round assignment → candidate **stays in the HR round** (reassignable to HR only).
- If the candidate's Onshore round **was skipped** (HR sent it straight to HR), and HR now unassigns → candidate **goes back to the Onshore round** (becomes assignable to Onshore again).

---

## 5. Open Questions / Ambiguities to Confirm

1. **HR-round unassign logic (Section 4, last row):** This is the one part of your explanation that reads as counter-intuitive — normally you'd expect "went through Onshore → unassign → back to HR only" and "skipped Onshore → unassign → back to HR only" to behave the *same* way, or for the skipped case to just stay in HR. Please confirm the two sub-cases above are exactly what you intend, since as described they're swapped from what most people would guess.
2. **"Pecans" in your recording** — I've read this as **"Manager/Management panel member."** Confirm that's correct.
3. Is **HR Admin** meant to have edit rights everywhere (not just view), including reassigning/unassigning at any stage, or view-only outside their own actions?
4. When a Manager "skips Tech Round 2" directly from *Pending with Tech Round 1* (before a Tech Round 1 result even exists) — does Tech Round 1 stay open in parallel (informational only, non-blocking), or does it get cancelled entirely?
5. Terminology check: you used both **"ABO"** and **"EBO"** for the group allowed to see a candidate once they enter — confirm these are the same term/typo and which one is correct.

---

*This document reflects your verbal walkthrough reorganized into a single state machine. Once you confirm the items in Section 5, I can turn this into a formal status-transition table or a build spec for the SharePoint list/workflow (e.g., Power Automate flow logic).*
