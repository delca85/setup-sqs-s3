locals {
  account_id = "161959740628"
  s3_bucket  = "arn:aws:s3:::my-bucket-for-playing"
}
# data "archive_file" "lambda_source_package" {
#   type        = "zip"
#   output_path = "${path.module}/tmp/lambda_sqs_s3.zip"

#   source_file = "${path.module}/lambda_sqs_s3_src/create_sqs.py"

#   excludes = [
#     "__pycache__",
#     "core/__pycache__",
#     "tests"
#   ]
# }

data "aws_iam_policy_document" "lambda_create_sqs_s3_put_get_notification" {
  statement {
    actions   = ["sqs:CreateQueue", "sqs:ListQueues"]
    resources = ["*"]
  }
  statement {
    actions = [
      "sqs:TagQueue",
      "sqs:GetQueueUrl",
      "sqs:AddPermission",
      "sqs:UntagQueue",
      "sqs:GetQueueAttributes",
      "sqs:ListQueueTags",
      "sqs:SetQueueAttributes"
    ]
    resources = ["arn:aws:sqs:eu-west-3:161959740628:*"]
  }

  statement {
    actions = [
      "s3:PutObject",
      "s3:PutObjectAcl",
      "s3:GetObject",
    ]
    resources = ["${local.s3_bucket}/*"]
  }
  statement {
    actions = [
      "s3:ListBucket",
      "s3:GetBucketNotification"
    ]
    resources = [local.s3_bucket]
  }
}

resource "aws_iam_policy" "lambda_create_sqs_s3_put_get_notification" {
  name   = "create-sqs-queue-s3-folder-s3-get-notification"
  policy = data.aws_iam_policy_document.lambda_create_sqs_s3_put_get_notification.json
}

module "lambda_function" {
  source = "terraform-aws-modules/lambda/aws"

  function_name = "create-sqs-and-s3"
  description   = "Create S3 Folder and SQS as destination of S3 events"
  handler       = "create_sqs.create_janus_queue_handler"
  runtime       = "python3.8"
  publish       = true
  attach_policy = true
  policy        = aws_iam_policy.lambda_create_sqs_s3_put_get_notification.arn

  source_path = "${path.module}/lambda_sqs_s3_src/create_sqs.py"

  layers = [
    module.lambda_layer_local.lambda_layer_arn
  ]

  tags = {
    Name = "marvel-lambda"
  }
}

module "lambda_layer_local" {
  source       = "terraform-aws-modules/lambda/aws"
  create_layer = true

  layer_name          = "sqs-s3-lambda-local-layer"
  description         = "Create S3 Folder and SQS as destination of S3 events function layer (deployed locally)"
  compatible_runtimes = ["python3.8"]

  source_path = "${path.module}/lambda_sqs_s3_src"
}
