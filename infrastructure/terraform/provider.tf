# The same configuration runs against the local emulator, the deployed stack,
# and real AWS in Phase 15. Only the endpoint changes.
#
# Terraform needs an explicit endpoint for every AWS service a configuration
# touches. Adding a resource for a service means adding it here too, otherwise
# the provider silently targets real AWS.
provider "aws" {
  region     = var.region
  access_key = "test"
  secret_key = "test"

  # Nothing here is a real AWS account, so skip the checks that look for one.
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true
  skip_region_validation      = true

  endpoints {
    sqs    = var.endpoint
    events = var.endpoint
    ses    = var.endpoint
  }
}
