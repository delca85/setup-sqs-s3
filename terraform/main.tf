module "lambda_function" {
  source = "terraform-aws-modules/lambda/aws"

  function_name = "createSqsAndS3Folder"
  description   = "Create S3 Folder and SQS as destination of S3 events"
  handler       = "create_sqs.create_janus_queue_handler"
  runtime       = "python3.8"
  publish       = true

  source_path = "../src/createSqsAndS3Folder/create_sqs"

  layers = [
    module.lambda_layer_local.arn
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

  source_path = "../src/createSqsAndS3Folder"
}