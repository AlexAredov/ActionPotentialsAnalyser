import os
import numpy as np
from io import StringIO

def combine_files(input_folder, output_file, quantity=None):
    combined_data = []
    current_time = 0
    processed_files = 0

    for file_name in os.listdir(input_folder):
        if file_name.endswith('.txt'):
            if quantity is not None and processed_files >= quantity:
                break

            file_path = os.path.join(input_folder, file_name)
            with open(file_path, 'r') as file:
                file_content = file.read()
            file_content = file_content.replace(',', '.')
            file_buffer = StringIO(file_content)

            try:
                data = np.genfromtxt(file_buffer, delimiter='\t')
            except ValueError as e:
                print(f"Ошибка в файле {file_name}: {e}")
                continue

            voltage = data[:, 1]

            time_interval = 0.00005
            time = np.arange(current_time, current_time + len(voltage) * time_interval, time_interval)
            time = time[:len(voltage)]

            combined_data.append(np.column_stack((time, voltage)))

            current_time = time[-1] + time_interval
            processed_files += 1

    combined_data = np.vstack(combined_data)
    np.savetxt(output_file, combined_data, delimiter='\t', fmt='%.6f')

input_folder = 'source'
output_file = 'source/combined.txt'
combine_files(input_folder, output_file, quantity=10)