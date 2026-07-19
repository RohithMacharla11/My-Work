const fields: Record<string, any> = {
  [`${targetPrefix}InterviewedById`]: user.id,
};
if (this.assignTargetRound === 'onshoreRound') {
  if (businessUnit) fields['OnshoreRoundDepartment'] = businessUnit;
  // Un-skip: bring the onshore round back to Pending so the workflow routes there
  fields['OnShoreRoundInterviewSelection'] = 'Pending';
}