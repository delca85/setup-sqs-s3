provider "aws" {
  region = "eu-west-3"
}
terraform {
  required_version = "1.0.7"
  backend "remote" {
    hostname     = "app.terraform.io"
    organization = "delca85-org"
    workspaces {
      name = "setup-sqs-s3"
    }
  }
}
