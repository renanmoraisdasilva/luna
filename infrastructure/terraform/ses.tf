# The sender identity Notification uses for customer email.
#
# This uses the SES v1 API (`VerifyEmailIdentity`) rather than v2, because the
# emulator's v2 REST surface rejects the dummy credentials Luna uses locally
# while the v1 Query API accepts them. Luna's Notification service sends through
# the v1 API as well, so identity and sending stay on the same surface.
#
# On AWS this identity has to be verified, and a production sender would use a
# verified domain with DKIM rather than a single address. The emulator accepts
# any address as verified immediately, which is why the value is a variable and
# why nothing else about sending changes between here and Phase 15.

resource "aws_ses_email_identity" "sender" {
  email = var.sender_address
}
