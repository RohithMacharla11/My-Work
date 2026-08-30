**Most Impactful GenAI / Agentic AI Solution: NexHireAI**

**Business Problem / Use Case**
Traditional hiring relies on static resumes and generic tests that don't reveal real-world job readiness. NexHireAI was built to replace that with dynamic, AI-generated skill assessments — adapting to each candidate's role and skill level — while giving recruiters data-driven insights instead of guesswork. It served two sides: candidates (skill validation, career guidance) and recruiters (evaluation, comparison, hiring decisions).

**Role & Responsibilities**
I worked as part of a 4-person team, contributing to the overall architecture and to the AI orchestration layer — designing how the app coordinates calls to the Gemini API for generating and scoring assessments, and integrating that with the Firebase backend and Next.js frontend.

**Technologies, Models, Frameworks, APIs**
- Frontend: Next.js 14, React, TypeScript, Tailwind CSS, ShadCN UI, Monaco Editor (live coding), Zustand
- Backend/Infra: Firebase Authentication (role-based: candidate/recruiter/admin), Firestore
- AI layer: **Google Genkit** orchestrating **Gemini API** calls through server-side TypeScript "flows"

**Agentic Capabilities**
This is where it goes beyond a single prompt-response wrapper:
- **Multi-step execution**: a candidate's action (e.g., starting an assessment) triggers a chain — generate role-specific questions → present mixed formats (MCQ/short-answer/code) → score subjective answers → update analytics — without manual intervention.
- **Tool/API integration**: Genkit flows act as callable tools the system invokes based on context — resume analysis, job recommendation, and learning-path generation are distinct AI-driven capabilities chained into one user journey.
- **Reasoning**: subjective/code answers are evaluated by the model against role-specific rubrics rather than simple pattern matching.
- **Workflow automation**: the entire candidate-to-recruiter pipeline (assess → analyze → report → recommend) runs with minimal human steps in between.

**Key Challenges**
- Keeping AI-generated assessments **consistent and fair** across 30+ roles without hallucinated or mismatched questions.
- Making the AI orchestration layer (Genkit flows) reliable and fast enough to feel real-time in a live coding editor.
- Structuring Firestore data so AI outputs (scores, analytics, recommendations) stayed in sync with a role-based, multi-user system.

**Lessons Learned**
- Server-side AI orchestration (rather than client-side prompt calls) is essential for security, consistency, and chaining multiple AI steps reliably.
- Designing clear "flow" boundaries (generate vs. score vs. recommend) made the system easier to debug and extend than one monolithic prompt.

**Measurable Outcomes**
As a team/portfolio-stage product rather than a live commercial deployment, I don't have production hiring metrics — I want to be upfront about that. What I can point to concretely: a working live deployment supporting **30+ role-specific assessment types**, a fully functional AI pipeline from assessment generation through scoring to career recommendations, and community traction on GitHub (multiple forks/stars), which validated the architecture as something others found reusable.